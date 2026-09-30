# Secrets

Every secret and every Chatwoot and Twenty setting is in one file per environment:

- `secrets/dev.env`: your machine.
- `secrets/prod.env`: production. Keep a copy in the password manager.

Both are git-ignored. `example.env` lists every key, with what it is and where to get it.

```bash
just secrets init dev    # make dev.env and generate the values that are random
just secrets init prod   # the same for prod.env
just secrets push        # send Spirit's keys in prod.env to GitHub
just secrets tidy dev    # put dev.env back in example.env's layout (backup: dev.env.bak)
```

Edit the file, then run the app again. Nothing else needs a copy.

`tidy` regroups a file to match `example.env` and keeps every value as it is. Keys `example.env`
does not list go last under `# --- Not in example.env ---`; a repeated key keeps its first value.

## Who reads what

| App | Keys | How |
| --- | --- | --- |
| SpiritAI | The names the app reads: `OPENAI_API_KEY`, `Goto__ClientId`, … | `just spirit run` loads `dev.env`. Production: `just secrets push` → GitHub → the "Sync Fly secrets" workflow → Fly |
| Chatwoot | `CHATWOOT_*` | Its recipes write them, without the prefix, into `chatwoot/.secrets.env` for compose. Settings and secrets alike |
| Twenty | `TWENTY_*` | The same, into `twenty/.secrets.env` |
| Cloudflare Tunnel | `CLOUDFLARE_*` | The same, into `cloudflared/.secrets.env`, from `prod.env` only |
| PostgreSQL | `POSTGRES_SERVER_*` | The same, into `postgres/.secrets.env`. `just postgres setup` also reads `CHATWOOT_DATABASE_URL` and `TWENTY_PG_DATABASE_URL` |

Chatwoot and Twenty have no `.env` of their own. Their settings (addresses, ports, email sender,
bucket place) are keys in this file with the app prefix. In dev, `compose.yaml` has a default for
each, so leave a key empty to use it. In prod there are no defaults: a missing required key stops
the recipe and names the key.

`just chatwoot setup` writes `Chatwoot__InboxIdentifier` and `Chatwoot__BotToken` into the file
itself.

## The file format

`KEY=value`, one per line. No quotes, no spaces around `=`, no `export`. Compose and
`just spirit run` read the value as written, so a quote becomes part of it.

## On the server

Copy `prod.env` to `secrets/prod.env` in the server's checkout. The app recipes read it from there.

## Production values GitHub does not have

`just secrets push` sends only the keys the Fly workflow reads. A GitHub secret cannot be read
back, so `prod.env` starts empty for those: fill in the ones you rotate. An empty key is skipped,
and GitHub keeps its old value.
