# A throwaway PostgreSQL to develop against, so the app never touches the real Neon database.
#
# Nothing is on a volume: `just db-down` wipes the lot. AgentCore migrates on startup, so a blank
# database is all the app needs. The container's own user is a superuser, which the first migration
# requires — it creates the `agentcore_writer` role.

container := "spirit-pg"
image := "postgres:17"

# 55432, so this does not fight a PostgreSQL already listening on 5432.
port := "55432"

user := "spirit"
password := "spirit"
database := "spirit"

pg_conn := "Host=localhost;Port=" + port + ";Database=" + database + ";Username=" + user + ";Password=" + password

# The secret AgentCore reads the connection string from.
#
# The dashed name and not POSTGRES_CONNECTION_STRING. `AgentCore:Secrets:postgres-connection-string`
# is already set in user secrets, pointing at Neon, and the configuration chain is read before the
# environment — so the plain variable is never reached. This overrides that exact key instead.
pg_secret := "AgentCore__Secrets__postgres-connection-string"

# List the recipes.
default:
    @just --list

# Start the throwaway PostgreSQL and wait until it answers.
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

# Remove the throwaway PostgreSQL and everything written to it.
db-down:
    -docker rm --force {{container}}

# Print the connection string.
db-url:
    @echo '{{pg_conn}}'

# Open a psql shell on the throwaway PostgreSQL.
db-shell:
    docker exec --interactive --tty {{container}} psql --username={{user}} --dbname={{database}}

# Run the host against the throwaway PostgreSQL, on http://localhost:5299/chat.
run: db-up
    cd src/SpiritAI && env "{{pg_secret}}={{pg_conn}}" dotnet run --launch-profile spirit
