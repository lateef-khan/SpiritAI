---
paths:
  - "secrets/**"
  - "*/justfile"
---

# Secrets

Read `secrets/README.md` before you add, rename, or move a secret.

- A secret or a setting of Chatwoot or Twenty goes in `secrets/<env>.env`, and its key name in
  `secrets/example.env`. An app has no `.env` of its own.
- Chatwoot, Twenty, and the tunnel read their keys with a prefix (`CHATWOOT_`, `TWENTY_`,
  `CLOUDFLARE_`). Their recipes strip it.
- Read key names from `example.env`. Do not print the contents of `dev.env`, `prod.env`, or any
  `.secrets.env`.
