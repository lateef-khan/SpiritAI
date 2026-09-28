---
paths:
  - "postgres/**"
  - "chatwoot/compose*.yaml"
  - "twenty/compose*.yaml"
---

# PostgreSQL server

Read `postgres/README.md` before you change the database server or how an app connects to it.

- Chatwoot and Twenty share one server, `spirit-postgres`, on the network `spirit-db`, in dev
  and in production. Each has its own database and role.
- Dev data is throwaway. Production data is on the volume `spirit-postgres-prod_data`. Never
  delete it. Run `just postgres backup` before any change that touches production data.
- An app URL in `secrets/prod.env` is the one source for that app's role, password, and database.
  `just postgres setup` reads it.
