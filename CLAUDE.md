# SpiritAI

A chat application built on the [AgentCore](https://github.com/MatthewHsu1/AgentCore) framework.
It started as a copy of `AgentCore/demo` and grows from there.

## Layout

- `src/SpiritAI/` — ASP.NET Core host. `Program.cs` wires up AgentCore
  (`AddAgentCoreHost` / `MapAgentCoreHost`) and serves the chat UI at `/chat`.
- `src/SpiritAI/config/` — agent pipelines as YAML. `spirit.yaml` is the app config
  (needs `OPENAI_API_KEY` in the environment). `example.yaml` is the annotated
  reference copied from AgentCore; treat it as documentation.
- `src/SpiritAI/Auth/` — sign-in. Verifies the Neon Auth token on `/v1`; see below.
- `src/web/` — React + Vite chat frontend (assistant-ui). The MSBuild target
  `BuildClientApp` in `SpiritAI.csproj` runs `npm ci && npm run build` and Vite writes
  the bundle into `src/SpiritAI/wwwroot/chat/` (gitignored).

## The AgentCore reference is switchable

This is the important thing to know about this repo. `Directory.Build.props` defines:

- `UseLocalAgentCore` — defaults to `true`. The app takes a `ProjectReference` to the
  AgentCore **source checkout** expected at `../AgentCore` (a sibling of this repo).
  Edit AgentCore, press F5 here, see the change. No pack, no version bump, and you can
  step into AgentCore code in the debugger. `SpiritAI.slnx` also includes the AgentCore
  projects so the IDE loads their source.
- `UseLocalAgentCore=false` — the app takes a `PackageReference` to the published
  `AgentCore.Hosting` NuGet package instead. This is for CI, to keep us honest against
  the real package: `dotnet build src/SpiritAI/SpiritAI.csproj -p:UseLocalAgentCore=false`.
  CI must build the **csproj, not the solution** — the solution includes the local
  AgentCore projects, which do not exist on a CI machine.

Caveat (as of 2026-08-27): `AgentCore.Hosting` is **not on nuget.org yet**, so package
mode fails to restore until the first AgentCore release is published. Local mode is
unaffected. When the package ships, update the `Version` in `SpiritAI.csproj`.

If your AgentCore checkout lives elsewhere, override the path:
`dotnet build -p:AgentCoreRoot=/path/to/AgentCore`.

## Sign-in

The browser signs in with Neon Auth (Managed Better Auth) — an emailed magic link, no
passwords, and no sign-up: only addresses configured in the Neon Console can get a
link. Turn sign-ups **off** there, or the "pre-configured only" rule does nothing.

Two settings, both the same Auth Base URL from Neon Console → Auth → Configuration:

- Browser: `src/web/.env` (copy `.env.example`), key `VITE_NEON_AUTH_URL`.
- Host: `Auth:Neon:BaseUrl`. Set it with user secrets rather than a checked-in file:

  ```bash
  cd src/SpiritAI && dotnet user-secrets set "Auth:Neon:BaseUrl" "https://ep-xxxx.neonauth.<region>.aws.neon.tech/neondb/auth"
  ```

The host refuses to start without it, naming the key. `src/SpiritAI/Auth/` then locks
`/v1` — `/health`, `/chat` and the sign-in page stay open.

Neon signs with EdDSA (Ed25519) and offers no alternative. Neither
`System.Security.Cryptography` nor `Microsoft.IdentityModel` implements that curve on
.NET 10, which is why `BouncyCastle.Cryptography` is a dependency and why the token
check in `Auth/` is hand-written rather than `AddJwtBearer`.

## Build & run

```bash
dotnet build                                   # full build, includes npm ci + vite build (needs Node)
dotnet build -p:SkipClientAppBuild=true        # server only, no Node needed
cd src/SpiritAI && dotnet run --launch-profile spirit   # http://localhost:5299/chat
```

The port is 5299 (the AgentCore demo uses 5199, so both can run at once).
For frontend work: `cd src/web && npm run dev` gives hot reload and proxies `/v1`
to the running host on 5299.

## Conventions

- .NET 10, nullable enabled, warnings are errors (`Directory.Build.props`).
- Config-first: agent behavior belongs in the YAML pipeline files.
