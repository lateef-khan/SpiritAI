#!/bin/bash
# The backup container's loop. Each day at BACKUP_HOUR_UTC: a full backup on Sunday, an
# incremental one on the other days. pgBackRest turns the first incremental into a full one.
set -uo pipefail

hour="${BACKUP_HOUR_UTC:-9}"
while true; do
    now="$(date -u +%s)"
    next="$(date -u -d "today ${hour}:00" +%s)"
    if [ "$next" -le "$now" ]; then
        next="$(date -u -d "tomorrow ${hour}:00" +%s)"
    fi
    sleep $((next - now))

    type=incr
    if [ "$(date -u +%u)" = 7 ]; then
        type=full
    fi
    pgbackrest backup --type="$type" || echo "The $type backup failed." >&2
done
