# SpiritAI

A chat application built on the [AgentCore](https://github.com/MatthewHsu1/AgentCore) framework.

Rules for C#, tests, and each company server app live in `.claude/rules/`. Each one loads when
you open a file it covers.

## Build and test

- `dotnet build` builds against the AgentCore checkout at `../AgentCore`, so an AgentCore edit
  shows up here with no pack. `-p:UseLocalAgentCore=false` builds against the NuGet package, as
  CI does.
- `dotnet build` fails on any warning. Fix the cause. Ask the owner before you add a `NoWarn` or
  a `#pragma warning disable`.
- `dotnet test` skips the database tests when no database is named. See `.claude/rules/tests.md`.

## Before you add an interface, wrapper, helper, or package

Read `.claude/reference/reuse-before-you-build.md` first.

## Layout

- `src/SpiritAI/` — ASP.NET Core host. `Program.cs` wires up AgentCore
  (`AddSpiritAgentCore` in `Hosting/AgentCoreExtensions.cs`, then `MapAgentCoreHost`) and
  serves the chat UI at `/chat`.
- `src/SpiritAI/config/` — agent pipelines as YAML. Agent behaviour belongs here, not in C#.
  `spirit.yaml` is the app config. `spirit.local.yaml` is written by `just spirit run`;
  do not edit it.
- `src/SpiritAI/Auth/` — sign-in. Verifies the Neon Auth token on `/v1`.
- `src/SpiritAI/Threads/` — the thread list, as REST over AgentCore's conversation store.
- `src/web/` — React + Vite chat frontend (assistant-ui); features live under
  `src/web/src/features/`. The MSBuild target `BuildClientApp` in `SpiritAI.csproj`
  runs `npm ci && npm run build` and Vite writes the bundle into
  `src/SpiritAI/wwwroot/chat/` (gitignored).
- `spirit-srv-030/` — sets up and updates the company server apps on SPIRITSRV-030 over ssh.
  Has a `README.md` and a `justfile`.
- `secrets/`, `chatwoot/`, `twenty/`, `postgres/`, `cloudflared/` — the company server apps and
  their secrets. Each folder has a `README.md` and a `justfile`.
