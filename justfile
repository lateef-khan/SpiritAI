# Every app has a folder with its own justfile. Run `just <app> <recipe>`; `just` lists them.

set dotenv-load

# SpiritAI recipes: `just spirit <recipe>`. See src/justfile.
mod spirit "src/justfile"

# Chatwoot recipes: `just chatwoot <recipe>`. See chatwoot/README.md.
mod chatwoot

# Cloudflare Tunnel recipes: `just cloudflared <recipe>`. See cloudflared/README.md.
mod cloudflared

# The PostgreSQL server Chatwoot and Twenty share: `just postgres <recipe>`. See postgres/README.md.
mod postgres

# Secrets of every app, one file per environment: `just secrets <recipe>`. See secrets/README.md.
mod secrets

# Twenty CRM recipes: `just twenty <recipe>`. See twenty/README.md.
mod twenty

# List the recipes.
default:
    @just --list

# The apps on the company server, in start order. Each is a folder with its own justfile.
# stack-down stops them in the reverse order, so the database stops last.
stack_apps := "postgres chatwoot twenty cloudflared"

# Production: start every app. An app that fails does not stop the others.
stack-up: (_stack "prod-up")

# Production: stop every app. Keeps their volumes.
stack-down: (_stack "prod-down" "reverse")

# Production: show the containers of every app.
stack-status: (_stack "prod-status")

_stack recipe order="forward":
    #!/usr/bin/env bash
    set -uo pipefail
    apps=({{stack_apps}})
    if [ "{{order}}" = "reverse" ]; then
        apps=($(printf '%s\n' "${apps[@]}" | tac))
    fi
    failed=()
    for app in "${apps[@]}"; do
        echo "== $app: {{recipe}}"
        just "$app" {{recipe}} || failed+=("$app")
    done
    if [ "${#failed[@]}" -gt 0 ]; then
        echo "{{recipe}} failed for: ${failed[*]}" >&2
        exit 1
    fi
    echo "{{recipe}} done for every app."
