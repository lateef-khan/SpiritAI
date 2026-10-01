# Twenty

Self-hosted [Twenty](https://github.com/twentyhq/twenty) CRM. We run our own image, built from
our fork of Twenty's source in `source/`. GitHub builds it and stores it on ghcr.io, and
`compose.yaml` pins its tag. Our extension apps go in `apps/`.

| Folder | Holds |
| --- | --- |
| `.` | How we run Twenty: compose files, recipes |
| `source/` | Git submodule: our fork of Twenty, branch `spirit`. See [Source code](#source-code) |
| `apps/` | Our extension apps, one folder each. See [apps/README.md](apps/README.md) |

Recipes live in `twenty/justfile`. From the repo root, run `just twenty <recipe>`. On the
server, run `just <recipe>` inside this folder.

## Local development

```bash
just twenty up      # starts, waits (the first start takes minutes)
just twenty down    # stops and deletes everything local
```

- Twenty: http://crm.spirit.localhost:53001 (the first visit asks you to make the admin user). The Hub
  needs the `crm.spirit.localhost` address, which is the default; a `TWENTY_SERVER_URL` in
  `secrets/dev.env` that says `localhost` overrides it (see [The Hub](#the-hub))
- Emails: the default `EMAIL_DRIVER=LOGGER` prints them in `just twenty logs`
- App code (logic functions) runs in the server and worker and calls the API at `SERVER_URL`.
  Inside Docker, the server answers to `crm.spirit.localhost` on port 53001 for that; in
  production the call goes out to the public address and back through the tunnel

`just twenty up source` builds the image from `source/` on your PC instead of pulling it. Use
it to check a change before a release. It takes 10 to 20 minutes and about 8 GB of RAM. For
daily coding, run Twenty's own dev mode in `source/` instead.

Settings and secrets live in `secrets/<env>.env` with the app prefix `TWENTY_`
(`just secrets init dev` makes the file). Twenty has no `.env` of its own. Every setting has a
local default in `compose.yaml`, so a fresh `dev.env` works as it is. Add `TWENTY_<NAME>=value`
to change one; `TWENTY_HOST_PORT` moves the port. `secrets/example.env` lists every key. `up` starts
the shared database server in `postgres/` first, the same as production. Nothing is kept: `down`
deletes redis, uploads, and Twenty's database, so every `up` starts clean.

## Production (company server)

`compose.prod.yaml` sits on top of `compose.yaml`. It is its own compose project
(`spirit-twenty-prod`), so the local `down` can never delete its volumes.

| | Local dev | Production |
| --- | --- | --- |
| Postgres | Throwaway container | Database `twenty` on the shared server in `postgres/`, required |
| Redis | Memory only | Append-only file on the `redis` volume, 512 MB cap, 1 GB container cap, `noeviction` |
| Uploads | Deleted by `down` | S3-compatible bucket (Backblaze B2), `TWENTY_STORAGE_S3_*`, required |
| Email | Printed in the logs | `TWENTY_EMAIL_*` in `secrets/prod.env` (Resend example in `secrets/example.env`), required |
| Container logs | Not capped | 3 files of 10 MB for each container |
| Host port | `127.0.0.1:53001` | None. Only the tunnel reaches the server, as `http://twenty:3000` |
| Logic functions (app code) | `LOCAL` by default | `TWENTY_LOGIC_FUNCTION_TYPE=LOCAL`, required |

Redis must use `noeviction`. Twenty keeps its job queues in redis, and an evicted key is a
lost job.

First setup on the server:

1. Copy this folder to the server. Install Docker and `just`. Give Twenty at least 2 GB of RAM.
2. Start the database server: `just postgres prod-up` (see `postgres/README.md`). It makes Twenty's
   own database, `twenty`, from `TWENTY_PG_DATABASE_URL`. Twenty makes many schemas in it
   (`core`, one `workspace_*` per workspace).
3. In `secrets/prod.env`, set `TWENTY_SERVER_URL` (the public HTTPS address), the
   `TWENTY_STORAGE_S3_*` bucket place and keys, the `TWENTY_EMAIL_*` values,
   `TWENTY_SPIRIT_HUB_*` and `TWENTY_LOGIC_FUNCTION_TYPE=LOCAL`. Production has no defaults: a
   missing required key stops `prod-up` and names the key.
4. Keep a copy of `TWENTY_ENCRYPTION_KEY` outside the server. Without it, the secrets in the
   database cannot be read.
5. `just prod-up`.
6. Open `TWENTY_SERVER_URL` and make the admin user. The first user is the server admin.

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
2. Do not put `crm.<domain>` behind Cloudflare Access. Spirit's server calls `/auth/spirit/users`
   there and the Hub frames it; Twenty's sign-in, which goes through the Hub, guards it.

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
  `{email, firstName, lastName}` answers `201 {"id"}` and sends no email. A new email gets a user
  with no password, in the one workspace, with the workspace's default role. An email Twenty
  already has is adopted, the same as Desk: the answer is that user's id, and its password and role
  stay as they are (a user outside the workspace joins it with the default role). This is how the
  owner's own Twenty admin gets linked. A wrong secret gets `401`.
- Every front-end page gets `Content-Security-Policy: frame-ancestors 'self' <SPIRIT_HUB_ORIGIN>`
  (only `'self'` when the origin is not set, so a missing setting breaks the Hub, not the guard).
- A full page load of `/welcome` goes to `SPIRIT_HUB_LOGIN_URL`. Twenty's front often reaches
  `/welcome` without a page load (a first visit with no session, a session lost mid-use), so the
  fork's sign-in page then does one full load of `/welcome` to let the server decide. When the
  document already was a load of `/welcome`, the server served it (no Hub set), and nothing
  reloads. `/welcome?local=1` still shows Twenty's own sign-in, for the back-door admin.
  This holds on an empty Twenty too: type `/welcome?local=1` by hand to make the first admin
  (Continue with Email → Sign up → Create workspace → Skip, profile, Skip). The whole sign-up
  stays on one page load, so it never drops to the Hub.
- `GET /spirit/sign-out` is a page that signs the CRM session out (the same `signOut` call as
  Twenty's own menu), tells other open CRM tabs, and posts `{ type: "hub:signed-out", app: "crm" }`
  to `SPIRIT_HUB_ORIGIN`. Like Desk's, it is a plain `GET`: any link to it signs the current CRM
  session out, which is an acceptable trade.

Settings: `TWENTY_SPIRIT_HUB_ORIGIN` and `TWENTY_SPIRIT_HUB_LOGIN_URL` in `secrets/<env>.env` (production:
`https://hub.spiritfitnessapps.com` and `https://hub.spiritfitnessapps.com/chat/login.html?app=crm`),
and `TWENTY_SPIRIT_HUB_SECRET` in the same file, equal to Spirit's `Twenty__HubSecret`.
Without the secret, both `/auth/spirit` endpoints answer 401. Without the origin, there is no
sign-out page and only the CRM itself may frame the CRM.

The origin of Spirit's `Hub__CrmUrl` must equal `TWENTY_SERVER_URL`'s origin, and `TWENTY_SPIRIT_HUB_ORIGIN`
must be the Hub's origin. The Hub and the CRM must be the same site for the session cookie to
reach the frame:
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
| Settings and secrets | `TWENTY_*` in `secrets/<env>.env`; see `secrets/README.md` |
| Local defaults | `compose.yaml`, as `${NAME:-default}` |
| Production differences | `compose.prod.yaml` |
| Settings that are not in `secrets/<env>.env` | Twenty's admin panel (`IS_CONFIG_VARIABLES_IN_DB_ENABLED`, on by default) |
| Changes to Twenty's own code | `source/`, branch `spirit` |
| Extension apps | `apps/<app>/` |
