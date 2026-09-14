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

# Personal values live in .env, git-ignored. See .env.example.
set dotenv-load

# The one developer the seed makes staff. Neon Auth owns the real table; locally it is a copy with
# this row in it, so a signed-in browser passes the staff gate. The email must be the one you sign
# in with; set SPIRIT_STAFF_EMAIL in .env when it is not your git email.
staff_email := env_var_or_default("SPIRIT_STAFF_EMAIL", `git config user.email`)
staff_name := env_var_or_default("SPIRIT_STAFF_NAME", `git config user.name`)

# List the recipes.
# TEMPORARY (2026-09-14): point at the AgentCore feat-harness worktree. Delete to revert to ../AgentCore.
agentcore_root := "/mnt/HDD/Projects/AgentCore/.claude/worktrees/feat-harness"

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

# Load dev/seed.pgsql: the staff sign-in and a few handoffs. Waits for the host to migrate first.
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

# Run the host on http://localhost:5299/chat against the throwaway PostgreSQL, and seed it once it has migrated.
run: db-up
    (just db-seed &)
    cd src/SpiritAI && env "{{pg_secret}}={{pg_conn}}" dotnet run --launch-profile spirit -p:AgentCoreRoot={{agentcore_root}}
