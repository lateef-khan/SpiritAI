# Every app has a folder with its own justfile. Run `just <app> <recipe>`; `just` lists them.

set dotenv-load

# SpiritAI recipes: `just spirit <recipe>`. See src/justfile.
mod spirit "src/justfile"

# Chatwoot recipes: `just chatwoot <recipe>`. See chatwoot/README.md.
mod chatwoot

# Cloudflare Tunnel recipes: `just cloudflared <recipe>`. See cloudflared/README.md.
mod cloudflared

# Twenty CRM recipes: `just twenty <recipe>`. See twenty/README.md.
mod twenty

# List the recipes.
default:
    @just --list

# The apps on the company server, in start order. Each is a folder with its own justfile.
stack_apps := "chatwoot twenty cloudflared"

# Production: start every app. An app that fails does not stop the others.
stack-up: (_stack "prod-up")

# Production: stop every app. Keeps their volumes.
stack-down: (_stack "prod-down")

# Production: show the containers of every app.
stack-status: (_stack "prod-status")

_stack recipe:
    #!/usr/bin/env bash
    set -uo pipefail
    failed=()
    for app in {{stack_apps}}; do
        echo "== $app: {{recipe}}"
        just "$app" {{recipe}} || failed+=("$app")
    done
    if [ "${#failed[@]}" -gt 0 ]; then
        echo "{{recipe}} failed for: ${failed[*]}" >&2
        exit 1
    fi
    echo "{{recipe}} done for every app."
