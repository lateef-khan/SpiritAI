# Twenty

Self-hosted [Twenty](https://github.com/twentyhq/twenty) CRM. We run our own image, built from
our fork of Twenty's source in `source/`. GitHub builds it and stores it on ghcr.io, and
`compose.yaml` pins its tag. Our extension apps go in `apps/`.

| Folder | Holds |
| --- | --- |
| `.` | How we run Twenty: compose files, `.env`, recipes |
| `source/` | Git submodule: our fork of Twenty, branch `spirit`. See [Source code](#source-code) |
| `apps/` | Our extension apps, one folder each. See [apps/README.md](apps/README.md) |

Recipes live in `twenty/justfile`. From the repo root, run `just twenty <recipe>`. On the
server, run `just <recipe>` inside this folder.

## Local development

```bash
just twenty up      # makes .env on first run, starts, waits (the first start takes minutes)
just twenty down    # stops and deletes everything local
```

- Twenty: http://crm.spirit.localhost:53001 (the first visit asks you to make the admin user). A
  `.env` made before the Hub still says `SERVER_URL=http://localhost:53001`; the Hub needs the
  `crm.spirit.localhost` one (see [The Hub](#the-hub))
- Emails: `EMAIL_DRIVER=LOGGER` prints them in `just twenty logs`

`just twenty up source` builds the image from `source/` on your PC instead of pulling it. Use
it to check a change before a release. It takes 10 to 20 minutes and about 8 GB of RAM. For
daily coding, run Twenty's own dev mode in `source/` instead.

Secrets are the `TWENTY_*` keys in `secrets/dev.env` (`just secrets init dev`). `up` starts
the shared database server in `postgres/` first, the same as production. Nothing is kept: `down`
deletes redis, uploads, and Twenty's database, so every `up` starts clean.

## Production (company server)

`compose.prod.yaml` sits on top of `compose.yaml`. It is its own compose project
(`spirit-twenty-prod`), so the local `down` can never delete its volumes.

| | Local dev | Production |
| --- | --- | --- |
| Postgres | Throwaway container | Database `twenty` on the shared server in `postgres/`, required |
| Redis | Memory only | Append-only file on the `redis` volume, 512 MB cap, 1 GB container cap, `noeviction` |
| Uploads | Deleted by `down` | S3-compatible bucket (Backblaze B2), `STORAGE_S3_*`, required |
| Email | Printed in the logs | `EMAIL_*` in `.env` (Resend example in `.env.example`) |
| Container logs | Not capped | 3 files of 10 MB for each container |
| Host port | `127.0.0.1:53001` | None. Only the tunnel reaches the server, as `http://twenty:3000` |

Redis must use `noeviction`. Twenty keeps its job queues in redis, and an evicted key is a
lost job.

First setup on the server:

1. Copy this folder to the server. Install Docker and `just`. Give Twenty at least 2 GB of RAM.
2. Start the database server: `just postgres prod-up` (see `postgres/README.md`). It makes Twenty's
   own database, `twenty`, from `TWENTY_PG_DATABASE_URL`. Twenty makes many schemas in it
   (`core`, one `workspace_*` per workspace).
3. `just env`, then edit `.env`: `SERVER_URL` (the public HTTPS address), the `STORAGE_S3_*`
   bucket place, and the `EMAIL_*` values. In `secrets/prod.env`: the `TWENTY_STORAGE_S3_*` keys
   and `TWENTY_EMAIL_SMTP_PASSWORD`.
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

## Release our image

The server never builds Twenty. It pulls a finished image from ghcr.io.

1. Commit and push our change in `source/` (branch `spirit`).
2. `just twenty release v2.41.0-spirit.2`. This tags the commit. A GitHub Action on the fork
   (`source/.github/workflows/spirit-image.yaml`) builds `ghcr.io/lateef-khan/twenty` with
   the same tag. It takes 20 to 40 minutes.
3. Set the new tag in `compose.yaml` (`x-twenty` → `image`).
4. Local: `just twenty down && just twenty up`. Commit `twenty/source` and `compose.yaml`.
5. Production: back up first (`just postgres backup`), then `just prod-update`.

A tag is `<Twenty version>-spirit.<n>`. Count `n` up for each release on the same Twenty
version. Never move a tag. To roll back, set the old tag and run `just prod-update`.

## Update Twenty

Twenty releases often. Use a tag that has a GitHub release, not only a Docker tag.

1. Read the release notes between the old and new tag. Since v1.23 you can skip versions.
2. `just twenty source-update v2.42.0` merges it into `spirit` and prints the next steps.
3. Release it as `v2.42.0-spirit.1`, the same as [any release](#release-our-image).

The server runs the schema upgrade each time it starts. Run `just postgres backup` before each
update in production.

## The Hub

The fork's `spirit-hub` module (`source/packages/twenty-server/src/engine/core-modules/spirit-hub/`)
lets the Spirit Hub sign people in to the CRM, make their CRM users, and hold the CRM in a frame.

- `GET /auth/spirit?note=…` takes a one-time note that Spirit signs (`HubNote.ForCrm`: HS256 with
  `SPIRIT_HUB_SECRET`, audience `crm`, 60 seconds). It sends the frame to Twenty's own
  `/verify?loginToken=…`, which finishes the sign-in. A bad, old, or used note gets 401.
- `POST /auth/spirit/users` with `Authorization: Bearer <SPIRIT_HUB_SECRET>` and
  `{email, firstName, lastName}` makes a user with no password, in the one workspace, with the
  workspace's default role. No email is sent. It answers `201 {"id"}`, `409` when Twenty already
  has the email (the Hub never takes over an existing user), and `401` for a wrong secret.
- Every front-end page gets `Content-Security-Policy: frame-ancestors 'self' <SPIRIT_HUB_ORIGIN>`.
- A full page load of `/welcome` (sign-out and a lost session both do one) goes to
  `SPIRIT_HUB_LOGIN_URL`. `/welcome?local=1` still shows Twenty's own sign-in, for the back-door
  admin.
- `GET /spirit/sign-out` is a page that signs the CRM session out (the same `signOut` call as
  Twenty's own menu), tells other open CRM tabs, and posts `{ type: "hub:signed-out", app: "crm" }`
  to `SPIRIT_HUB_ORIGIN`. Like Desk's, it is a plain `GET`: any link to it signs the current CRM
  session out, which is an acceptable trade.

Settings: `SPIRIT_HUB_ORIGIN` and `SPIRIT_HUB_LOGIN_URL` in `.env` (production:
`https://hub.spiritfitnessapps.com` and `https://hub.spiritfitnessapps.com/chat/login.html?app=crm`),
and `TWENTY_SPIRIT_HUB_SECRET` in `secrets/<env>.env`, equal to Spirit's `Twenty__HubSecret`.
Without the secret, both `/auth/spirit` endpoints answer 401. Without the origin, there is no
sign-out page and no frame header, so browsers let any site frame the CRM.

The Hub and the CRM must be the same site for the session cookie to reach the frame:
`hub.spiritfitnessapps.com` and `crm.spiritfitnessapps.com`, or `hub.spirit.localhost` and
`crm.spirit.localhost` locally.

## Source code

`source/` is a git submodule, which means it is a separate git repo inside this one. It points
at our fork of Twenty on GitHub. SpiritAI stores only which commit of the fork to use. Our
changes live on the fork's branch `spirit`. Twenty's released tags (`twenty/vX.Y.Z`) are
merged into that branch, so our changes stay.

Twenty's code is AGPL-3.0, except for the files marked as enterprise code. Change only the
AGPL code, and do not copy or unlock the enterprise code.

| Recipe | Does |
| --- | --- |
| `source-add <fork-url>` | One time. Makes `spirit` from the tag in `compose.yaml` and pushes it to the fork |
| `source-init` | On a fresh clone of SpiritAI: downloads `source/` |
| `source-update <tag>` | Merges a released tag, e.g. `v2.42.0`, into `spirit`. Prints the next steps |
| `release <tag>` | Tags the pushed `spirit` commit, e.g. `v2.41.0-spirit.2`. GitHub builds the image |

First setup:

1. On GitHub, fork `twentyhq/twenty`.
2. `just twenty source-add <fork-url>`
3. Commit `.gitmodules` and `twenty/source` in SpiritAI.

When a merge stops on a conflict, both sides changed the same lines. Fix the files in
`source/`, then `git add` them and `git commit` inside `source/`. To keep conflicts small, put
our code in new files and change as few of Twenty's lines as we can.

The source is blobless: git has the whole history, and it downloads each file's contents the
first time something needs them.

## Where things go

| What | Where |
| --- | --- |
| Version | `compose.yaml`, `x-twenty` → `image` |
| How the image is built | `source/.github/workflows/spirit-image.yaml` |
| Build from source locally | `compose.source.yaml` |
| Settings | `.env` (git-ignored), `.env.example` (documented defaults) |
| Production differences | `compose.prod.yaml` |
| Settings that are not in `.env` | Twenty's admin panel (`IS_CONFIG_VARIABLES_IN_DB_ENABLED`, on by default) |
| Changes to Twenty's own code | `source/`, branch `spirit` |
| Extension apps | `apps/<app>/` |
