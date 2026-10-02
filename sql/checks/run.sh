#!/usr/bin/env bash
# Runs one part of run-checks.ps1 on SPIRITSRV-024 (ssh host `spirit`). The six SpiritAI SQL
# passwords go from secrets/dev.env to the script's standard input; they never reach a command
# line or a terminal.
# Usage: sql/checks/run.sh logins|snapshot-before|snapshot-after|custservice|sage
set -euo pipefail

part=${1:?usage: sql/checks/run.sh logins|snapshot-before|snapshot-after|custservice|sage}
here="$(cd "$(dirname "$0")" && pwd)"
env_file="$here/../../secrets/dev.env"

scp -q "$here/run-checks.ps1" spirit:run-checks.ps1

for key in DAB_CUSTSERVICE_SQL_PASSWORD CUSTSERVICE_SQL_MANAGER_PASSWORD CUSTSERVICE_SQL_ADMIN_PASSWORD \
           DAB_SAGE_SQL_PASSWORD SAGE_SQL_MANAGER_PASSWORD SAGE_SQL_ADMIN_PASSWORD; do
    value=$(grep -m1 "^$key=" "$env_file" | cut -d= -f2-)
    if [ -z "$value" ]; then
        echo "secrets/dev.env has no $key" >&2
        exit 1
    fi
    printf '%s\n' "$value"
done | ssh spirit "powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File run-checks.ps1 -Part $part"
