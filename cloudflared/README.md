# Cloudflare Tunnel

The one public door for the apps on this server. `cloudflared` dials out to Cloudflare, so
the server needs no open port and no public IP. We run the official image, pinned in
`compose.yaml`.

Recipes live in `cloudflared/justfile`. From the repo root, run `just cloudflared <recipe>`.
On the server, run `just <recipe>` inside this folder.

## First setup

1. Put the domain on Cloudflare DNS.
2. In the Cloudflare dashboard, go to **Zero Trust → Networks → Tunnels**. Make a tunnel of
   type **Cloudflared**.
3. Copy the token from its install command (the long string after `--token`).
4. Start Chatwoot first (`just chatwoot prod-up`). The tunnel joins Chatwoot's network.
5. `just env`, then paste the token into `.env` as `TUNNEL_TOKEN`.
6. `just up`.
7. In the tunnel's **Public Hostname** tab, add one row per app (the table below).

| Hostname | Service | Public? |
| --- | --- | --- |
| `desk.<domain>` | `http://web:3000` (Chatwoot) | Staff only |
| `chat.<domain>` | `http://host.docker.internal:5299` (Spirit, if it runs on the host) | Yes |
| `crm.<domain>` | Twenty, when it exists | Staff only |
| `hub.<domain>` | The app shell, when it exists | Staff only |

After step 7, set Chatwoot's `FRONTEND_URL` to `https://desk.<domain>` and run
`just chatwoot prod-update`.

## How the tunnel reaches an app

Inside a container, `localhost` is the container itself, not the server.

- An app in Docker: the tunnel joins that app's compose network. Use the service name and the
  port **inside** the container, such as `http://web:3000`.
- An app on the host: `http://host.docker.internal:<port>`. The app must listen on Docker's
  host address, not only on `localhost`, and the firewall must let Docker's networks in.

To add a Docker app, add its network under `networks:` in `compose.yaml`, the same way as
Chatwoot.

## Cloudflare settings to check

- **Caching:** do not cache HTML on these hostnames. Cloudflare must pass the apps' frame
  headers (`X-Frame-Options`, `Content-Security-Policy`) through unchanged.
- **WebSockets:** on (the default). Chatwoot's live inbox needs them.
- **Staff-only hostnames:** put them behind Cloudflare Access. Leave the public chat and the
  Chatwoot widget paths open, or customers cannot use them.

| Recipe | Does |
| --- | --- |
| `up` | Start and wait until Cloudflare has the tunnel |
| `down` | Stop. The apps keep running without a public door |
| `update` | Pull the pinned image again and restart |
| `logs` | Follow the logs |

## Update cloudflared

1. Read the release notes between the old and new tag.
2. Change the tag in `compose.yaml`.
3. `just update`.
