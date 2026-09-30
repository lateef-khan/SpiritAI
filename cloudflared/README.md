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
4. Paste the token into `secrets/prod.env` as `CLOUDFLARE_TUNNEL_TOKEN` (`just secrets init prod`
   makes the file).
5. `just up`. No app has to run first.
6. In the tunnel's **Public Hostname** tab, add one row per app (the table below).

| Hostname | Service | Public? |
| --- | --- | --- |
| `desk.<domain>` | `http://chatwoot:3000` (Chatwoot) | Staff only |
| `crm.<domain>` | `http://twenty:3000` (Twenty) | Staff only |

`chat.<domain>` and `hub.<domain>` are not tunnel rows. Spirit runs on Fly; they are DNS records
that point at `spiritai.fly.dev`.

After step 6, set `CHATWOOT_FRONTEND_URL` in `secrets/prod.env` to `https://desk.<domain>` and run
`just chatwoot prod-update`. Set `TWENTY_SERVER_URL` to `https://crm.<domain>` and run
`just twenty prod-update`.

## How the tunnel reaches an app

Inside a container, `localhost` is the container itself, not the server.

- An app in Docker: the app joins the shared network `spirit-edge` under a name of its own.
  Use that name and the port **inside** the container, such as `http://chatwoot:3000`.
- An app on the host: `http://host.docker.internal:<port>`. The app must listen on Docker's
  host address, not only on `localhost`, and the firewall must let Docker's networks in.

The tunnel joins only `spirit-edge`. Every `up` recipe creates it when it is missing, and no
`down` recipe deletes it. So the tunnel starts with no app running, and an app that is down
shows a Cloudflare error page on its own hostname only.

To add a Docker app, give its web service this in the app's `compose.yaml`, with a name of
its own in `aliases`:

```yaml
    networks:
      default:
      edge:
        aliases: [myapp]

networks:
  edge:
    external: true
    name: spirit-edge
```

## Cloudflare settings to check

- **Caching:** do not cache HTML on these hostnames. Cloudflare must pass the apps' frame
  headers (`X-Frame-Options`, `Content-Security-Policy`) through unchanged.
- **WebSockets:** on (the default). Chatwoot's live inbox needs them.
- **Staff-only hostnames:** put them behind Cloudflare Access, except `desk.`, `crm.` and `hub.`:
  the Hub frames Desk and CRM and Spirit's server calls them. Leave the public chat and the
  Chatwoot widget paths open, or customers cannot use them.

| Recipe | Does |
| --- | --- |
| `up` (`prod-up`) | Start and wait until Cloudflare has the tunnel |
| `down` (`prod-down`) | Stop. The apps keep running without a public door |
| `status` (`prod-status`) | Show the container |
| `update` | Pull the pinned image again and restart |
| `logs` | Follow the logs |

The `prod-*` names let the root `just stack-*` recipes treat the tunnel like every other app.

## Update cloudflared

1. Read the release notes between the old and new tag.
2. Change the tag in `compose.yaml`.
3. `just update`.
