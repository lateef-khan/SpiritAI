# Throwaway PostgreSQL and MinIO to develop against. Nothing is on a volume; `db-down` and
# `blob-down` wipe the lot. AgentCore migrates on startup, so a blank database is enough.

container := "spirit-pg"
image := "postgres:17"
port := "55432"
user := "spirit"
password := "spirit"
database := "spirit"
pg_conn := "Host=localhost;Port=" + port + ";Database=" + database + ";Username=" + user + ";Password=" + password

# Dashed, not POSTGRES_CONNECTION_STRING: user secrets already set this exact key to Neon and
# win over the environment, so only overriding the same key takes effect.
pg_secret := "AgentCore__Secrets__postgres-connection-string"

blob_container := "spirit-s3"
# Quay, because Docker Hub stopped serving minio/minio in 2025.
blob_image := "quay.io/minio/minio"
blob_port := "59000"
blob_key := "spirit"
blob_secret := "spiritspirit"
blob_bucket := "spirit"
blob_endpoint := "http://localhost:" + blob_port
blob_key_secret := "AgentCore__Secrets__s3-access-key-id"
blob_secret_secret := "AgentCore__Secrets__s3-secret-access-key"

# spirit.yaml with its blobs: line pointed at MinIO. Git-ignored; rewritten on every run.
local_config := "config/spirit.local.yaml"

set dotenv-load

# Who the seed makes staff. Must be the email you sign in with; override in .env if needed.
staff_email := env_var_or_default("SPIRIT_STAFF_EMAIL", `git config user.email`)
staff_name := env_var_or_default("SPIRIT_STAFF_NAME", `git config user.name`)

# List the recipes.
default:
    @just --list

# Start PostgreSQL and wait until it answers.
db-up:
    #!/usr/bin/env bash
    set -euo pipefail

    if [ -n "$(docker ps --quiet --filter 'name=^{{container}}$')" ]; then
        echo "{{container}} is already up on port {{port}}."
        exit 0
    fi

    docker rm --force {{container}} >/dev/null 2>&1 || true

    docker run --detach --name {{container}} \
        --env POSTGRES_USER={{user}} \
        --env POSTGRES_PASSWORD={{password}} \
        --env POSTGRES_DB={{database}} \
        --publish {{port}}:5432 \
        {{image}} >/dev/null

    for _ in $(seq 1 60); do
        if docker exec {{container}} pg_isready --username={{user}} --dbname={{database}} >/dev/null 2>&1; then
            echo "{{container}} is ready on port {{port}}."
            exit 0
        fi
        sleep 1
    done

    echo "{{container}} never became ready. Try: docker logs {{container}}" >&2
    exit 1

# Remove PostgreSQL and everything in it.
db-down:
    -docker rm --force {{container}}

# Print the connection string.
db-url:
    @echo '{{pg_conn}}'

# Open psql.
db-shell:
    docker exec --interactive --tty {{container}} psql --username={{user}} --dbname={{database}}

# Load dev/seed.pgsql once the host has migrated.
db-seed:
    #!/usr/bin/env bash
    set -euo pipefail

    for _ in $(seq 1 180); do
        if docker exec {{container}} psql --username={{user}} --dbname={{database}} --tuples-only --no-align \
            --command="select to_regclass('spirit.handoff')" | grep -q handoff; then
            docker exec --interactive {{container}} psql --username={{user}} --dbname={{database}} --quiet \
                --set=ON_ERROR_STOP=1 --set=staff_email='{{staff_email}}' --set=staff_name='{{staff_name}}' \
                < dev/seed.pgsql
            echo "Seeded {{database}}: {{staff_email}} is staff, and the inbox has rows."
            exit 0
        fi
        sleep 1
    done

    echo "spirit.handoff never appeared; is the host running?" >&2
    exit 1

# Start MinIO, wait until it answers, and make the bucket.
blob-up:
    #!/usr/bin/env bash
    set -euo pipefail

    if [ -z "$(docker ps --quiet --filter 'name=^{{blob_container}}$')" ]; then
        docker rm --force {{blob_container}} >/dev/null 2>&1 || true

        docker run --detach --name {{blob_container}} \
            --env MINIO_ROOT_USER={{blob_key}} \
            --env MINIO_ROOT_PASSWORD={{blob_secret}} \
            --publish {{blob_port}}:9000 \
            {{blob_image}} server /data >/dev/null
    fi

    for _ in $(seq 1 60); do
        if curl --silent --fail {{blob_endpoint}}/minio/health/live >/dev/null 2>&1; then
            # mc runs inside the container, so it talks to the container's own port.
            docker exec {{blob_container}} mc alias set local http://localhost:9000 {{blob_key}} {{blob_secret}} >/dev/null
            docker exec {{blob_container}} mc mb --ignore-existing local/{{blob_bucket}} >/dev/null
            echo "{{blob_container}} is ready on {{blob_endpoint}}, bucket {{blob_bucket}}."
            exit 0
        fi
        sleep 1
    done

    echo "{{blob_container}} never became ready. Try: docker logs {{blob_container}}" >&2
    exit 1

# Remove MinIO and everything in it.
blob-down:
    -docker rm --force {{blob_container}}

# List the bucket.
blob-ls:
    docker exec {{blob_container}} mc ls --recursive local/{{blob_bucket}}

# Write config/spirit.local.yaml with blobs: pointed at MinIO.
local-config:
    #!/usr/bin/env bash
    set -euo pipefail
    cd src/SpiritAI

    if ! grep -q '^  blobs: ' config/spirit.yaml; then
        echo "config/spirit.yaml has no top-level 'blobs:' line under providers: to swap." >&2
        exit 1
    fi

    sed 's|^  blobs: .*|  blobs: { kind: s3, endpoint: {{blob_endpoint}}, bucket: {{blob_bucket}} }|' \
        config/spirit.yaml > {{local_config}}

# Run the host on http://localhost:5299/chat against PostgreSQL and MinIO, and seed it.
run: db-up blob-up local-config
    (just db-seed &)
    cd src/SpiritAI && env "{{pg_secret}}={{pg_conn}}" \
        "{{blob_key_secret}}={{blob_key}}" "{{blob_secret_secret}}={{blob_secret}}" \
        ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5299 \
        AgentCore__ConfigurationPath={{local_config}} \
        dotnet run --no-launch-profile
