#!/bin/sh
# Bring the Machine onto the tailnet, then hand off to the app.
#
# A Fly Machine is a Firecracker microVM, not a shared-kernel container, so a real
# TUN device works here and no SOCKS proxy is needed: 100.98.168.6 becomes an
# ordinary route and HttpClient reaches it with no code change. If Tailscale ever
# fails to bring the interface up on Fly, the fallback is
# `tailscaled --tun=userspace-networking --socks5-server=localhost:1055` plus
# ALL_PROXY, which also needs a NO_PROXY list for OpenAI, Qdrant and Neon.
#
# Horizontal scaling: this runs once per Machine, and every Machine is its own
# tailnet node. The hostname carries FLY_MACHINE_ID so two Machines never collide
# (Tailscale would otherwise silently rename the second one to <name>-1), and the
# tag is what the ACL on the DAB server should grant, rather than a node name that
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

exec "$@"
