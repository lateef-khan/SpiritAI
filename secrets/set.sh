#!/usr/bin/env bash
# Sets one key in secrets/<env>.env: replaces its line, or adds it at the end.
#
#   secrets/set.sh <dev|prod> <KEY> <value>
set -euo pipefail

file="$(dirname "$0")/$1.env" key="$2" value="$3"

if [ ! -f "$file" ]; then
    echo "$file is missing. Run: just secrets init $1" >&2
    exit 1
fi

if grep -q "^$key=" "$file"; then
    umask 077
    KEY="$key" VALUE="$value" awk \
        'index($0, ENVIRON["KEY"] "=") == 1 { print ENVIRON["KEY"] "=" ENVIRON["VALUE"]; next } { print }' \
        "$file" > "$file.tmp"
    mv "$file.tmp" "$file"
else
    printf '%s=%s\n' "$key" "$value" >> "$file"
fi
