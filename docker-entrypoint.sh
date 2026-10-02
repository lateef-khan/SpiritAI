#!/bin/sh
# Start the SQL listeners for the agent's shell, then hand off to the app.
set -e

# The agent's shell reaches the two SQL Servers on 127.0.0.1 through Cloudflare Access. Each
# listener runs in a loop, so a listener that dies comes back instead of leaving the shell with
# "connection refused" until the Machine restarts.
if [ -n "${CF_ACCESS_CLIENT_ID}" ] && [ -n "${CF_ACCESS_CLIENT_SECRET}" ]; then
    export TUNNEL_SERVICE_TOKEN_ID="${CF_ACCESS_CLIENT_ID}"
    export TUNNEL_SERVICE_TOKEN_SECRET="${CF_ACCESS_CLIENT_SECRET}"
    for pair in sql-custservice:1433 sql-sage:1434; do
        name="${pair%%:*}" port="${pair#*:}"
        (
            # The entrypoint runs under set -e; without this a listener's non-zero exit ends the loop.
            set +e
            while true; do
                cloudflared access tcp --hostname "${name}.spiritfitnessapps.com" --url "127.0.0.1:${port}"
                echo "cloudflared: ${name} listener exited; restarting in 5s" >&2
                sleep 5
            done
        ) &
    done
    echo "cloudflared: SQL listeners on 127.0.0.1:1433 (CustService) and 127.0.0.1:1434 (Sage)"
else
    echo "cloudflared: CF_ACCESS_CLIENT_ID or CF_ACCESS_CLIENT_SECRET is not set; no SQL listeners" >&2
fi

exec "$@"
