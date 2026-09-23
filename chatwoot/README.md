# Chatwoot

Self-hosted [Chatwoot](https://github.com/chatwoot/chatwoot). It is the staff inbox and the
conversation store for human handoff. We run the official image, pinned in `Dockerfile`.
We do not fork its code.

Recipes live in `chatwoot/justfile`. From the repo root, run `just chatwoot <recipe>`. On
the server, run `just <recipe>` inside this folder.

## Local development

```bash
just chatwoot up      # makes .env on first run, builds, migrates, starts, waits
just chatwoot down    # stops and deletes everything local
```

- Chatwoot: http://localhost:53000 (the first visit asks you to make the admin user)
- Caught emails: http://localhost:58025

With `DATABASE_URL` empty, a throwaway postgres starts too. Nothing is kept: postgres,
redis, and uploads are deleted by `down`, so every `up` starts clean.

Other recipes: `logs`, `console`.

## Production (company server)

`compose.prod.yaml` sits on top of `compose.yaml`. It is its own compose project
(`spirit-chatwoot-prod`), so the local `down` can never delete its volumes.

| | Local dev | Production |
| --- | --- | --- |
| Postgres | Throwaway container | `DATABASE_URL` (Neon), required |
| Redis | Memory only | Append-only file on the `redis` volume, 512 MB cap, `noeviction` |
| Uploads | Deleted by `down` | S3-compatible bucket (Backblaze B2), `STORAGE_*`, required |
| Email | Mailpit | `SMTP_*` in `.env` (Resend example in `.env.example`) |

First setup on the server:

1. Copy this folder to the server. Install Docker and `just`.
2. `just env`, then edit `.env`: `DATABASE_URL`, `FRONTEND_URL` (the public HTTPS address),
   `REDIS_PASSWORD`, the `STORAGE_*` bucket values, and the `SMTP_*` and `MAILER_SENDER_EMAIL` values.
3. On the database, run `create schema chatwoot;` once. Use Neon's direct host, not `-pooler`.
4. `just prod-up`.

| Recipe | Does |
| --- | --- |
| `prod-up` | Start everything |
| `prod-update` | Rebuild, migrate, restart web and worker. Redis keeps running |
| `prod-down` | Stop everything. Keeps the volumes |
| `prod-logs`, `prod-console` | Logs, Rails console |

Web listens on `127.0.0.1:53000` only. Put the server's HTTPS proxy in front of it.

## Update Chatwoot

1. Read the release notes between the old and new tag.
2. Change the tag in `Dockerfile`.
3. Local: `just chatwoot down && just chatwoot up`. Production: `just prod-update`.

## Where things go

| What | Where |
| --- | --- |
| Version | `Dockerfile` `FROM` tag |
| Settings | `.env` (git-ignored), `.env.example` (documented defaults) |
| Production differences | `compose.prod.yaml` |
| Local schema setup | `local-db.sql` |
| Small code patches | `Dockerfile`, below `FROM`. Last resort |
| Webhooks, agent bot, Dashboard Apps, inboxes | `setup/` scripts that call the Chatwoot API (to come) |
| Webhook receivers | `src/SpiritAI` (to come) |

## Tables in their own schema

`DATABASE_URL` carries `schema_search_path=chatwoot,public`, so Chatwoot's tables go in the
`chatwoot` schema and it can share a database with SpiritAI. Upstream does not document this
setup. Test each version update against a Neon branch before production.
