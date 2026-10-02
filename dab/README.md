# DAB

Data API Builder serves the CustService database and Sage invoices as MCP tools to SpiritAI's agents. It runs on
SPIRITSRV-030 as the container `dab` on `spirit-edge`. The Cloudflare tunnel publishes it at
`https://dab.spiritfitnessapps.com/mcp`, behind Cloudflare Access: only the `spiritai` service
token gets through.

Recipes live in `dab/justfile`. From the repo root: `just dab <recipe>`. On the server:
`just <recipe>` inside this folder.

## Secrets

`DAB_CUSTSERVICE_SQL_PASSWORD` in `secrets/prod.env`: the password of the SQL login named in
`dab-config.json`. The config reads it as `@env('CUSTSERVICE_SQL_PASSWORD')`.

`DAB_SAGE_SQL_PASSWORD`: the password of `spiritai_dab` on Sage, read as
`@env('SAGE_SQL_PASSWORD')` by `dab-config.sage.json`.

## First setup

1. Fill `DAB_CUSTSERVICE_SQL_PASSWORD` and `DAB_SAGE_SQL_PASSWORD` and run `just spirit-srv-030 secrets`.
2. `just spirit-srv-030 run dab prod-up`.
3. `just spirit-srv-030 run dab probe` lists the tools.
4. The tunnel row and Access app: see [cloudflared/README.md](../cloudflared/README.md).

## Change the config

1. Edit `dab-config.json` (CustService) or `dab-config.sage.json` (Sage invoices; the first
   file names it in `data-source-files`). An entity description must say what each `THROW`
   number of its procedure means, because DAB hides the text.
2. Commit, then `just spirit-srv-030 deploy dab`. `update` recreates the container, so the new
   config loads.
3. `just spirit-srv-030 run dab probe`.

DAB has no live reload for entities. A bad config stops the container; `just spirit-srv-030 logs dab`
shows why.

## Update DAB

1. Read the release notes between the old and new tag.
2. Change the tag in `compose.yaml` and the `$schema` URL in `dab-config.json`.
3. Deploy, then probe.
