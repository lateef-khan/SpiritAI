---
paths:
  - "cloudflared/**"
  - "*/compose*.yaml"
---

# Cloudflare Tunnel and compose files

Read `cloudflared/README.md` before you change the tunnel or give an app a public hostname.

Every app reaches the tunnel through the shared Docker network `spirit-edge`. Production apps
publish no host port. The section **How the tunnel reaches an app** shows the compose block a new
app needs.

Each app's `compose.prod.yaml` runs as its own compose project, so a local `down` cannot delete
production volumes. Keep the project names apart.
