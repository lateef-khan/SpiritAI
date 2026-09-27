# SpiritAI

A chat application built on [AgentCore](https://github.com/MatthewHsu1/AgentCore).

## Quick start

Needs .NET 10, Node, and an AgentCore checkout at `../AgentCore` (sibling of this repo).

```bash
just secrets init dev    # then fill in secrets/dev.env
just spirit run
# open http://localhost:5299/chat
```

Every secret is in `secrets/dev.env` (local) and `secrets/prod.env` (production). See
`secrets/README.md`.

By default the app builds against the local AgentCore source (fast dev loop, debuggable).
Pass `-p:UseLocalAgentCore=false` to build against the published `AgentCore.Hosting`
NuGet package instead — that is what CI and the `Dockerfile` do, and it needs no sibling
checkout. The switch is in `Directory.Build.props`.

## Deploy

Deploys happen in CI: merging to `main` runs `.github/workflows/deploy.yml`, which builds,
tests, creates the Fly app if needed, syncs secrets and deploys. Nothing to run locally.

`just secrets push` sends them from `secrets/prod.env`. The repository secrets it needs: `FLY_API_TOKEN`, `OPENAI_API_KEY`, `OPENCODE_GO_API_KEY`, `QDRANT_API_KEY`,
`POSTGRES_CONNECTION_STRING`, `TS_AUTHKEY`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`
(the Backblaze B2 keyID and applicationKey), `CUSTSERVICE_SQL_PASSWORD` (the
`SpiritReadOnlyUser` password the analytics desk's shell reads; the same value DAB holds as
`DAB_SQL_PASSWORD` on SPIRITSRV-024). `GRAFANA_CLOUD_INSTANCE_ID` and
`GRAFANA_CLOUD_API_TOKEN` are optional, and optional together.

`./tailscale-setup.sh` walks you through the Tailscale side and proves it works before you
merge. One Fly app serves both the UI and the API; its settings are in `fly.toml`.

## Company server apps

Chatwoot, Twenty, their PostgreSQL server, and the Cloudflare tunnel run on the company server. Each app is a folder
with its own `compose.yaml`, `.env`, `justfile`, and `README.md`. An app's settings are in
its own `.env`. Its secrets are in `secrets/prod.env` (see `secrets/README.md`).

```bash
just stack-up       # start every app
just stack-status   # show every app's containers
just stack-down     # stop every app (keeps the volumes)
```

An app that fails does not stop the others; the recipe names it at the end. The list of
apps is `stack_apps` in the root `justfile`. The apps share one Docker network,
`spirit-edge`, and have no port on the host; see `cloudflared/README.md`. Chatwoot and Twenty
each keep a database on the one PostgreSQL server; see `postgres/README.md`.
