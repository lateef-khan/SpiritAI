#!/usr/bin/env bash
# Writes one app's secrets from secrets/<env>.env into a file of its own, with the app's prefix
# taken off: CHATWOOT_REDIS_PASSWORD becomes REDIS_PASSWORD. Empty values are left out, so an
# unset secret stays unset in the container. The app's justfile runs this before every compose
# command and hands the result to compose.
#
#   secrets/export.sh <dev|prod> <PREFIX_> <output file> [KEY_TO_LEAVE_OUT ...]
set -euo pipefail

env="$1" prefix="$2" out="$3"
shift 3

source_file="$(dirname "$0")/$env.env"
if [ ! -f "$source_file" ]; then
    echo "$source_file is missing. Run: just secrets init $env" >&2
    exit 1
fi

umask 077
{
    echo "# Written by secrets/export.sh from secrets/$env.env. Edit that file, not this one."
    # An app with every key empty is fine: grep finds nothing, and the file has only the header.
    { grep -E "^${prefix}[A-Z0-9_]+=.+" "$source_file" || true; } | while IFS= read -r line; do
        key="${line%%=*}"
        for skipped in "$@"; do
            [ "$key" = "$skipped" ] && continue 2
        done
        echo "${line#"$prefix"}"
    done
} > "$out"
