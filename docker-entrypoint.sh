#!/bin/sh
# Bring the Machine onto the tailnet, then hand off to the app.
#
# Nothing in config/spirit.yaml uses the tailnet any more; Tailscale is still installed and
# started here, and is removed in a later step.
#
# A Fly Machine is a Firecracker microVM, not a shared-kernel container, so a real
# TUN device works here and no SOCKS proxy is needed. If Tailscale ever fails to bring the
# interface up on Fly, the fallback is
# `tailscaled --tun=userspace-networking --socks5-server=localhost:1055` plus
# ALL_PROXY, which also needs a NO_PROXY list for OpenAI, Qdrant and Neon.
#
# Horizontal scaling: this runs once per Machine, and every Machine is its own
# tailnet node. The hostname carries FLY_MACHINE_ID so two Machines never collide
# (Tailscale would otherwise silently rename the second one to <name>-1), and the
# tag is what a tailnet ACL should grant, rather than a node name that
# changes on every deploy.
set -e

if [ -z "${TS_AUTHKEY}" ]; then
    echo "FATAL: TS_AUTHKEY is not set. Add it as a GitHub repository secret; the deploy workflow syncs it to Fly." >&2
    exit 1
fi

mkdir -p /dev/net /var/run/tailscale /var/lib/tailscale
if [ ! -c /dev/net/tun ]; then
    mknod /dev/net/tun c 10 200
    chmod 600 /dev/net/tun
fi

tailscaled \
    --state=/var/lib/tailscale/tailscaled.state \
    --socket=/var/run/tailscale/tailscaled.sock &

# A Machine boots with an empty state file every time (no volume), so `up` has to
# be given the key on every start. An ephemeral key is what makes that safe: the
# node deletes itself when the Machine stops, so a scaled-down or auto-slept
# Machine does not leave a dead host behind.
tailscale up \
    --authkey="${TS_AUTHKEY}" \
    --hostname="${TS_HOSTNAME:-spiritai}-${FLY_MACHINE_ID:-local}" \
    --advertise-tags="${TS_TAGS:-tag:spiritai}" \
    --accept-routes \
    --accept-dns=false

echo "tailscale: up as ${TS_HOSTNAME:-spiritai}-${FLY_MACHINE_ID:-local}"
tailscale status || true

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
