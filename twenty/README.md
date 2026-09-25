# Twenty

Self-hosted [Twenty](https://github.com/twentyhq/twenty) CRM. We run the official image,
pinned in `compose.yaml`. We do not fork its code.

Recipes live in `twenty/justfile`. From the repo root, run `just twenty <recipe>`. On the
server, run `just <recipe>` inside this folder.

## Local development

```bash
just twenty up      # makes .env on first run, starts, waits (the first start takes minutes)
just twenty down    # stops and deletes everything local
```

- Twenty: http://localhost:53001 (the first visit asks you to make the admin user)
- Emails: `EMAIL_DRIVER=LOGGER` prints them in `just twenty logs`

Secrets are the `TWENTY_*` keys in `secrets/dev.env` (`just secrets init dev`). With
`TWENTY_PG_DATABASE_URL` empty, a throwaway postgres starts too. Nothing is kept: postgres,
redis, and uploads are deleted by `down`, so every `up` starts clean.

## Production (company server)

`compose.prod.yaml` sits on top of `compose.yaml`. It is its own compose project
(`spirit-twenty-prod`), so the local `down` can never delete its volumes.

| | Local dev | Production |
| --- | --- | --- |
| Postgres | Throwaway container | `TWENTY_PG_DATABASE_URL` (Neon), required |
| Redis | Memory only | Append-only file on the `redis` volume, 512 MB cap, 1 GB container cap, `noeviction` |
| Uploads | Deleted by `down` | S3-compatible bucket (Backblaze B2), `STORAGE_S3_*`, required |
| Email | Printed in the logs | `EMAIL_*` in `.env` (Resend example in `.env.example`) |
| Container logs | Not capped | 3 files of 10 MB for each container |
| Host port | `127.0.0.1:53001` | None. Only the tunnel reaches the server, as `http://twenty:3000` |

Redis must use `noeviction`. Twenty keeps its job queues in redis, and an evicted key is a
lost job.

First setup on the server:

1. Copy this folder to the server. Install Docker and `just`. Give Twenty at least 2 GB of RAM.
2. On Neon, make a database of its own: `create database twenty;`. Twenty makes many schemas
   (`core`, one `workspace_*` per workspace), so it does not share SpiritAI's database.
3. `just env`, then edit `.env`: `SERVER_URL` (the public HTTPS address), the `STORAGE_S3_*`
   bucket place, and the `EMAIL_*` values. In `secrets/prod.env`: `TWENTY_PG_DATABASE_URL`
   (Neon's direct host, not `-pooler`), the `TWENTY_STORAGE_S3_*` keys, and
   `TWENTY_EMAIL_SMTP_PASSWORD`.
4. Keep a copy of `TWENTY_ENCRYPTION_KEY` outside the server. Without it, the secrets in the
   database cannot be read.
5. `just prod-up`.
6. Open `SERVER_URL` and make the admin user. The first user is the server admin.

| Recipe | Does |
| --- | --- |
| `prod-up` | Start everything |
| `prod-update` | Pull the pinned image, upgrade the schema, restart server and worker. Redis keeps running |
| `prod-down` | Stop everything. Keeps the redis volume |
| `prod-status` | Show the containers |
| `prod-logs` | Follow the logs |

## Put it behind the tunnel

The server joins the shared network `spirit-edge` as `twenty`. The tunnel does not need
Twenty to run, and Twenty does not need the tunnel.

1. In the tunnel's **Public Hostname** tab: `crm.<domain>` → `http://twenty:3000`.
2. Put `crm.<domain>` behind Cloudflare Access. It is staff only.

## Update Twenty

Twenty releases often. Use a tag that has a GitHub release, not only a Docker tag.

1. Read the release notes between the old and new tag. Since v1.23 you can skip versions.
2. Change the tag in `compose.yaml` (`x-twenty` → `image`).
3. Production: back up first. On Neon, make a branch; it is the backup.
4. Local: `just twenty down && just twenty up`. Production: `just prod-update`.

The server runs the schema upgrade each time it starts. Test each update against a Neon
branch before production.

## Where things go

| What | Where |
| --- | --- |
| Version | `compose.yaml`, `x-twenty` → `image` |
| Settings | `.env` (git-ignored), `.env.example` (documented defaults) |
| Production differences | `compose.prod.yaml` |
| Settings that are not in `.env` | Twenty's admin panel (`IS_CONFIG_VARIABLES_IN_DB_ENABLED`, on by default) |
