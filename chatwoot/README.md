# Chatwoot

Self-hosted [Chatwoot](https://github.com/chatwoot/chatwoot). It is the staff inbox and the
conversation store for human handoff. We run the official image, pinned in `Dockerfile`.
We do not fork its code.

Recipes live in `chatwoot/justfile`. From the repo root, run `just chatwoot <recipe>`. On
the server, run `just <recipe>` inside this folder.

## Local development

```bash
just chatwoot up      # builds, migrates, starts, waits
just chatwoot down    # stops and deletes everything local
```

- Chatwoot: http://localhost:53000 (the first visit asks you to make the admin user)
- Caught emails: http://localhost:58025

Settings and secrets live in `secrets/<env>.env` with the app prefix `CHATWOOT_`
(`just secrets init dev` makes the file). Chatwoot has no `.env` of its own. Every setting has a
local default in `compose.yaml`, so a fresh `dev.env` works as it is. Add
`CHATWOOT_<NAME>=value` to change one; `CHATWOOT_HOST_PORT` and `CHATWOOT_MAILPIT_PORT` move the
two ports. `secrets/example.env` lists every key. `up` starts
the shared database server in `postgres/` first, the same as production. Nothing is kept: `down`
deletes redis, uploads, and Chatwoot's database, so every `up` starts clean.

Other recipes: `logs`, `console`.

## Production (company server)

`compose.prod.yaml` sits on top of `compose.yaml`. It is its own compose project
(`spirit-chatwoot-prod`), so the local `down` can never delete its volumes.

| | Local dev | Production |
| --- | --- | --- |
| Postgres | Throwaway container | Database `chatwoot` on the shared server in `postgres/`, required |
| Redis | Memory only | Append-only file on the `redis` volume, 512 MB cap, `noeviction` |
| Uploads | Deleted by `down` | S3-compatible bucket (Backblaze B2), `CHATWOOT_STORAGE_*`, required |
| Email | Mailpit | `CHATWOOT_SMTP_*` in `secrets/prod.env` (Resend example in `secrets/example.env`) |
| Host port | `127.0.0.1:53000` | None. Only the tunnel reaches web, as `http://chatwoot:3000` |

First setup on the server:

1. Copy this folder to the server. Install Docker and `just`.
2. In `secrets/prod.env`, set `CHATWOOT_FRONTEND_URL` (the public HTTPS address), the
   `CHATWOOT_STORAGE_*` bucket place and keys, and the `CHATWOOT_SMTP_*` and
   `CHATWOOT_MAILER_SENDER_EMAIL` values. Set the Hub settings too (see "The Spirit inbox" below):
   `CHATWOOT_SPIRIT_HUB_ORIGIN=https://hub.spiritfitnessapps.com`,
   `CHATWOOT_SPIRIT_HUB_LOGIN_URL=https://hub.spiritfitnessapps.com/chat/login.html?app=desk`,
   `CHATWOOT_LOGOUT_REDIRECT_LINK=https://hub.spiritfitnessapps.com/chat/login.html?app=desk`.
   Production has no defaults: a missing required key stops `prod-up` and names the key.
3. Start the database server: `just postgres prod-up` (see `postgres/README.md`). It makes Chatwoot's
   database from `CHATWOOT_DATABASE_URL`.
4. `just prod-up`.
5. In `secrets/prod.env`, set `CHATWOOT_ADMIN_TOKEN`, `Chatwoot__AccountId`,
   `Chatwoot__BaseUrl=https://desk.<domain>`, and `Chatwoot__ServiceToken`. Then `just setup prod`.
   It writes the inbox identifier and the bot token into the same file.

| Recipe | Does |
| --- | --- |
| `prod-up` | Start everything |
| `prod-update` | Rebuild, migrate, restart web and worker. Redis keeps running |
| `prod-down` | Stop everything. Keeps the volumes |
| `prod-status` | Show the containers |
| `prod-logs`, `prod-console` | Logs, Rails console |

Web joins the shared network `spirit-edge` as `chatwoot`. The Cloudflare tunnel is its public
door; see `cloudflared/README.md`.

## Update Chatwoot

1. Read the release notes between the old and new tag.
2. Change the tag in `Dockerfile`.
3. Local: `just chatwoot down && just chatwoot up`. Production: `just postgres backup`, then
   `just prod-update`.
4. `just check-contact-guard <inbox identifier>` (add the public URL on production). It must say OK.
   If Chatwoot will not start and names `spirit_public_contact_guard.rb`, the guard needs a fix.
5. `just check-hub <desk-url> <hub-origin>`. It must say all checks passed.

## Where things go

| What | Where |
| --- | --- |
| Version | `Dockerfile` `FROM` tag |
| Settings and secrets | `CHATWOOT_*` in `secrets/<env>.env`; see `secrets/README.md` |
| Local defaults | `compose.yaml`, as `${NAME:-default}` |
| Production differences | `compose.prod.yaml` |
| Small code patches | `initializers/`, copied in by `Dockerfile`. Last resort |
| Spirit's API inbox, agent bot, and teams | `just setup` (`setup/spirit-inbox.sh`) |
| Spirit's name, logo, and brand links | `just setup` (`setup/branding.sh`, logo in `brand/`) |

## The Spirit inbox

`just setup` keeps these, and each one matters:

- **No webhook.** The inbox's `webhook_url` is empty. The widget hears Chatwoot's own socket,
  and the AI reads what it missed from Chatwoot on its next turn.
- **The agent bot stays connected.** A new conversation starts `pending`, which is the AI's,
  and a resolved one comes back `pending` when the visitor writes again.
- **The out-of-office message stays empty.** The handoff skill tells a person what happens
  while the office is closed. Chatwoot would post its own message as well.

## Staff and push popups

A GoTo ring posts a note that mentions the member of staff, or their team. Chatwoot tells a
mentioned user only when that user is an administrator or a member of the Spirit inbox. `just setup`
adds only the service user. Add every member of staff to the inbox (Settings → Inboxes →
Spirit Chat/Phone → Collaborators) and to their team (Settings → Teams).

The popup is Chatwoot's browser push. Each member of staff turns it on one time:
see [staff-call-alerts.md](staff-call-alerts.md).

Push needs VAPID keys (the key pair that signs each push). Chatwoot makes them on the first page
load and keeps them in the database, in `installation_configs` (`VAPID_KEYS`). The browsers'
push subscriptions are in the same database (`notification_subscriptions`). A redeploy keeps
both. `VAPID_PUBLIC_KEY` / `VAPID_PRIVATE_KEY` are read only when the row is missing, which
means a new database, which has no subscriptions either. So we do not set them.

## Its own database

Chatwoot has a database of its own, `chatwoot`, on the PostgreSQL server in `postgres/`. It
shares the server with Twenty, not the database. Its tables are in the default `public` schema.
Run `just postgres backup` before each version update: the update changes the schema when
Chatwoot starts.

## The Hub

`initializers/spirit_hub.rb` lets the Spirit Hub hold Desk in a frame, and sends anyone who is
not signed in to the Hub instead of Chatwoot's own sign-in page.

- A response that refuses framing outright (`X-Frame-Options`, and no `Content-Security-Policy`
  of its own) gets `Content-Security-Policy: frame-ancestors 'self' <SPIRIT_HUB_ORIGIN>` instead.
  A response that already carries its own `Content-Security-Policy` — a web widget inbox with
  `allowed_domains`, for example — is left exactly as Chatwoot made it.
- A plain `GET /app/login` (no `sso_auth_token`, no `?local=1`) redirects to
  `SPIRIT_HUB_LOGIN_URL`, the Hub's sign-in page for Desk. `?local=1` still shows Chatwoot's own
  form, for the back-door admin. A repeated or trailing slash on `/app/login` cannot bypass the
  match: the path is collapsed and trimmed first.
- `GET /spirit/sign-out` is a page that deletes the Chatwoot session, then posts
  `{ type: "hub:signed-out", app: "desk" }` to `SPIRIT_HUB_ORIGIN`. It is a plain `GET`, with no
  token and no confirmation: any link or image tag pointed at it signs the current Desk session
  out. That is an acceptable trade — signing a person out is not a destructive action — and
  framing Desk at all is limited to `SPIRIT_HUB_ORIGIN` by the `Content-Security-Policy` above.

Three plain settings, in `secrets/<env>.env` with the `CHATWOOT_` prefix: `CHATWOOT_SPIRIT_HUB_ORIGIN`,
`CHATWOOT_SPIRIT_HUB_LOGIN_URL`, and Chatwoot's own `CHATWOOT_LOGOUT_REDIRECT_LINK` (where it
sends a visitor after they sign out of the dashboard; set it to the same address as the login
URL). Chatwoot sees them without the prefix. `spirit_hub.rb` reads `SPIRIT_HUB_ORIGIN` and
`SPIRIT_HUB_LOGIN_URL` with `ENV.fetch`: either one missing stops Chatwoot's web container from
starting at all. `LOGOUT_REDIRECT_LINK` is Chatwoot's own setting and fails soft (falls back to
`/`), so a miss there is a wrong redirect, not a crash. Production requires all three in
`secrets/prod.env`; local dev defaults them to `hub.spirit.localhost`.

`just setup` also makes or finds a Platform App named "Spirit Hub" and gives it the account, so
the Hub can call the Platform API to make and sign in Desk users. It writes
`Chatwoot__PlatformToken`, `Chatwoot__AdminToken`, and `Chatwoot__InboxId` into
`secrets/<env>.env`, the same way it writes the inbox identifier and bot token.

Run `just check-hub <desk-url> <hub-origin>` after every version change. It must say all checks
passed.

## The public contact guard

`initializers/spirit_public_contact_guard.rb` changes one Chatwoot rule. The public contact routes
(`/public/api/v1/inboxes/{inbox}/contacts`) keep the name and drop the email, phone number, and
identifier.

Without it, anyone with the inbox identifier (it is public) can send a stranger's email or phone
and get back the stranger's contact: name, email, and phone. Our widget sends only a `source_id`,
and Spirit sets phone and email with the staff API, so the guard takes nothing from us.

The `/api/v1/widget` routes have the same gap, but they need a Website inbox. We have none.
