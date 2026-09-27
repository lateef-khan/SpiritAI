# PostgreSQL

The one database server for Chatwoot and Twenty, in local dev and on the company server. Each app
has a database and a role of its own on it. We run the official pgvector image, pinned in
`compose.yaml`.

Recipes live in `postgres/justfile`. From the repo root, run `just postgres <recipe>`. On the
server, run `just <recipe>` inside this folder.

## How the apps reach it

The server joins the Docker network `spirit-db` as `postgres`. Chatwoot's and Twenty's containers
join the same network and connect to `postgres:5432`. The server has no port on the host, and it
is not on `spirit-edge`, so the tunnel cannot reach it.

| App | Database | Role | URL in `secrets/<env>.env` |
| --- | --- | --- | --- |
| Chatwoot | `chatwoot` | `chatwoot` | `CHATWOOT_DATABASE_URL` |
| Twenty | `twenty` | `twenty` | `TWENTY_PG_DATABASE_URL` |

Each role can open only its own database. The superuser (`postgres`) is for the recipes.

`setup` reads each URL, then makes or updates that app's role, password, database, and
extensions. It is safe to run again.

## Local development

`just chatwoot up` and `just twenty up` start this server first, so you rarely call it yourself.

- Leave `CHATWOOT_DATABASE_URL`, `TWENTY_PG_DATABASE_URL`, and
  `POSTGRES_SERVER_SUPERUSER_PASSWORD` empty in `secrets/dev.env`. Each app then gets
  `<app>:<app>@postgres:5432/<app>`, and the superuser password is `postgres`.
- The data is in an anonymous volume. Nothing is kept.
- An app's `down` drops that app's database. The other app keeps running.
- `just postgres down` stops the server and deletes every database.

| Recipe | Does |
| --- | --- |
| `up` | Start, wait, and run `setup dev` |
| `down` | Stop and delete every database |
| `drop <database>` | Delete one app's database. Refuses on the production server |
| `logs` | Follow the logs |

## Production (company server)

`compose.prod.yaml` sits on top of `compose.yaml`. It is its own compose project
(`spirit-postgres-prod`), and the data is on its volume `spirit-postgres-prod_data`. No recipe
deletes that volume.

One machine runs either the dev server or the production one, never both: both use the container
name `spirit-postgres`.

First setup:

1. In `secrets/prod.env`, set these three keys. `just secrets init prod` generates them in a
   new file. In a file you already have, generate each one:

   ```bash
   secrets/set.sh prod POSTGRES_SERVER_SUPERUSER_PASSWORD "$(openssl rand -hex 24)"
   secrets/set.sh prod CHATWOOT_DATABASE_URL "postgres://chatwoot:$(openssl rand -hex 24)@postgres:5432/chatwoot"
   secrets/set.sh prod TWENTY_PG_DATABASE_URL "postgres://twenty:$(openssl rand -hex 24)@postgres:5432/twenty"
   ```

2. `just postgres prod-up`. It starts the server, runs `setup prod`, and sets up backups when S3
   is set (see Backups).
3. Start the apps: `just chatwoot prod-up` and `just twenty prod-up`. Or start everything with
   `just stack-up` from the repo root. `stack-down` stops the apps first and this server last.

`POSTGRES_SERVER_SUPERUSER_PASSWORD` is read only the first time the volume starts empty. To
change it later, change it in `psql` too: `ALTER ROLE postgres PASSWORD '...'`.

To change an app's password, change its URL in `secrets/prod.env`, run `just setup prod`, then
`just <app> prod-update`.

| Recipe | Does |
| --- | --- |
| `prod-up` | Start, wait, run `setup prod`, and turn backups on or off (see Backups) |
| `prod-down` | Stop. Keeps every database. Stop the apps first |
| `prod-status` | Show the container |
| `prod-update` | Pull the pinned image again and restart |
| `prod-logs` | Follow the logs |
| `setup [dev\|prod]` | Make or update each app's role, database, and extensions |
| `psql [database]` | Open `psql` as the superuser |

## Backups

Production only. Local dev has no backups.

[pgBackRest](https://pgbackrest.org) backs up the whole server, both apps, to an S3 bucket. It
is in our image (`Dockerfile`). Backups are on when `POSTGRES_SERVER_S3_ENDPOINT` is set in
`secrets/prod.env`, and off when it is empty.

- **Every day at 09:00 UTC** the `backup` container makes one backup: a full one on Sunday, an
  incremental one on the other days. An incremental backup copies only the files that changed.
- **Between backups**, PostgreSQL sends each finished WAL file (its log of changes) to the
  bucket. A restore can go back to any second covered by that log.
- **Compression:** `zst`. **Encryption:** `aes-256-cbc`, with `POSTGRES_SERVER_BACKUP_CIPHER_PASS`.
- **Retention:** four full backups. pgBackRest deletes older ones, with their incremental
  backups and WAL.
- **If S3 is down,** WAL waits on the server's disk. Past 4 GiB it is dropped, so the disk does
  not fill. A restore then cannot reach a time inside that gap.

Keep a copy of `POSTGRES_SERVER_BACKUP_CIPHER_PASS` outside the server. Without it, no backup can
be restored. Never change it.

To turn backups on:

1. Make a bucket for backups only, and an application key with read and write on it.
2. Fill in the `POSTGRES_SERVER_S3_*` keys in `secrets/prod.env` (see `secrets/example.env`).
   `just secrets init prod` generates `POSTGRES_SERVER_BACKUP_CIPHER_PASS`. In a file you
   already have: `secrets/set.sh prod POSTGRES_SERVER_BACKUP_CIPHER_PASS "$(openssl rand -base64 48)"`.
3. `just prod-up`. It creates the repository in the bucket, checks that WAL reaches it, and
   starts the `backup` container. The first backup is a full one.

| Recipe | Does |
| --- | --- |
| `backup [incr\|diff\|full]` | Back up now. Run it before each Chatwoot or Twenty update |
| `backups` | List the backups, with sizes and times |
| `prod-restore [time]` | Put the whole server back, to the latest state or to a UTC time |
| `prod-logs` | The `backup` container's log shows each daily backup |

### Restore

A restore puts back the **whole server**: Chatwoot and Twenty go back to the same moment.

```bash
just chatwoot prod-down
just twenty prod-down
just postgres prod-restore                            # the latest state
just postgres prod-restore "2026-09-27 08:00:00+00"   # or a UTC time
just chatwoot prod-up
just twenty prod-up
```

`prod-restore` works on a new machine too, with an empty volume. It needs only `secrets/prod.env`.

## Update PostgreSQL

A new minor version (the same `pg16`): `just prod-update`.

A new major version (`pg16` to `pg17`) cannot read the old data files, and pgBackRest restores
only into the version that made the backup. Plan it as its own job: dump each database with
`pg_dump`, start the new version on a new volume, restore the dumps, then run
`pgbackrest stanza-upgrade` and a full backup.

## Update pgBackRest

The `Dockerfile` pins its version. When the build fails because that version is gone, read the
release notes, change the version, and run `just prod-update`.
