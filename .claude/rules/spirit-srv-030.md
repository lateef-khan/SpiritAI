---
paths:
  - "spirit-srv-030/**"
---

# SPIRITSRV-030

Read `spirit-srv-030/README.md` before you change how code or secrets reach the server.

- The server gets committed code only, by `git archive`. The shipped paths are the `paths`
  variable in the justfile. A new folder an app needs on the server goes there.
- A deploy must never write or delete `.env`, `.secrets.env`, `secrets/prod.env`, or a volume.
  Only the `secrets` recipe writes `secrets/prod.env`.
