# SPIRITSRV-030

Set up and update the company server apps (Postgres, Chatwoot, Twenty, DAB, the tunnel) on
SPIRITSRV-030 from your laptop, over ssh. Nothing here runs on the server by itself.

Recipes live in `spirit-srv-030/justfile`. From the repo root, run `just spirit-srv-030 <recipe>`.

The server is the ssh alias `spiritsrv-030` (in `~/.ssh/config`). It is set in one place, `host`
at the top of the justfile. Another server is a change to that line.

## How the code gets there

The server has no GitHub login. `sync` sends the committed code with `git archive`, over ssh, into
`~/spirit` on the server. It sends only what the server needs: the root `justfile`,
`src/justfile`, `spirit-srv-030/`, `postgres/`, `chatwoot/`, `cloudflared/`, `dab/`, `secrets/`, and `twenty/` without
`source/` and `apps/`. Twenty is not built there; it pulls its image from ghcr.io.

- Only committed code ships. `sync` refuses when those paths have uncommitted changes.
- `~/spirit/DEPLOYED` says which commit is on the server.
- These files live only on the server, and a deploy never writes or deletes them: each app's
  `.secrets.env`, `secrets/prod.env`, and the Docker volumes. Only the `secrets`
  recipe writes `secrets/prod.env`.
- A file you delete from the repo stays on the server until you delete it there.

## First setup

1. `just secrets init prod` on your laptop, and fill in `secrets/prod.env`. Each app README says
   which keys.
2. `just spirit-srv-030 setup`. It installs `just` on the server with `ssh -t`, so `sudo` asks
   for the password in your terminal. Then it ships the code.
3. `just spirit-srv-030 secrets`. It copies `secrets/prod.env` to the server.
4. `just spirit-srv-030 run postgres prod-up`. See [postgres/README.md](../postgres/README.md).
5. `just spirit-srv-030 run chatwoot prod-up`, then its setup steps. See
   [chatwoot/README.md](../chatwoot/README.md).
6. `just spirit-srv-030 run twenty prod-up`. See [twenty/README.md](../twenty/README.md). Make the
   first admin at `https://crm.spiritfitnessapps.com/welcome?local=1`.
7. `just spirit-srv-030 run dab prod-up`. See [dab/README.md](../dab/README.md).
8. `just spirit-srv-030 run cloudflared up`, then the public hostnames. See
   [cloudflared/README.md](../cloudflared/README.md).

After the first setup, `just spirit-srv-030 stack-up` starts everything in order.

## Update

```bash
just spirit-srv-030 deploy chatwoot   # ship HEAD, then run the update recipe
just spirit-srv-030 deploy twenty v1.2   # or ship a branch, tag, or commit
```

Back up first for Twenty and Postgres: `just spirit-srv-030 run postgres backup`.

## Recipes

| Recipe | Does |
| --- | --- |
| `setup [ref]` | One time. Installs this laptop's `just` version on the server if it is missing or different, makes `~/spirit`, ships the code |
| `sync [ref]` | Ships the committed code. `ref` is a branch, tag, or commit; the default is `HEAD` |
| `secrets` | Copies `secrets/prod.env` to the server, mode 600. Overwrites the server's copy. Prints no values |
| `secrets-pull` | Copies the server's `secrets/prod.env` back to the laptop (the old copy becomes `prod.env.bak`). Run it after `run chatwoot setup prod` |
| `deploy <app> [ref]` | `sync`, then the app's update recipe: `prod-update`, or `update` for `cloudflared` and `dab` |
| `run <app> <recipe...>` | Runs any recipe of an app on the server, e.g. `run twenty prod-up` |
| `status` | Shows the shipped commit and the containers of every app |
| `logs <app>` | Follows the logs of one app |
| `stack-up`, `stack-down` | The root recipes of the same name, run on the server |

`<app>` is `postgres`, `chatwoot`, `twenty`, `dab`, or `cloudflared`.
