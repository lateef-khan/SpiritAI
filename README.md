# SpiritAI

A chat application built on [AgentCore](https://github.com/MatthewHsu1/AgentCore).

## Quick start

Needs .NET 10, Node, and an AgentCore checkout at `../AgentCore` (sibling of this repo).

```bash
export OPENAI_API_KEY=sk-...
cd src/SpiritAI
dotnet run --launch-profile spirit
# open http://localhost:5299/chat
```

By default the app builds against the local AgentCore source (fast dev loop, debuggable).
Pass `-p:UseLocalAgentCore=false` to build against the published `AgentCore.Hosting`
NuGet package instead — that is what CI and the `Dockerfile` do, and it needs no sibling
checkout. See `CLAUDE.md` for the full story.

## Deploy

Deploys happen in CI: merging to `main` runs `.github/workflows/deploy.yml`, which builds,
tests, creates the Fly app if needed, syncs secrets and deploys. Nothing to run locally.

The repository secrets it needs: `FLY_API_TOKEN`, `OPENAI_API_KEY`, `QDRANT_API_KEY`,
`POSTGRES_CONNECTION_STRING`, `TS_AUTHKEY`. `GRAFANA_CLOUD_INSTANCE_ID` and
`GRAFANA_CLOUD_API_TOKEN` are optional, and optional together.

`./tailscale-setup.sh` walks you through the Tailscale side and proves it works before you
merge. One Fly app serves both the UI and the API — see the Fly.io section of `CLAUDE.md`.
