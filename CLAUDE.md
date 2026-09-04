# SpiritAI

A chat application built on the [AgentCore](https://github.com/MatthewHsu1/AgentCore) framework.
It started as a copy of `AgentCore/demo` and grows from there.

## File size

Keep each `.cs` file under 400 lines.

**Scope**
- Apply this to files you are **already editing** for the task. Do not go hunting.
- If a file you must edit is over 400 lines, split it first, then make your change.
- Skip generated files and `*.schema.json`.

**Rules**
- One public type per file. Name the file after the type.
- A private helper type gets its own file when it passes 20 lines.
- Split by responsibility. Move one cohesive group of members into its own
  class, in the same namespace and folder.
- A split that only moves lines does not count. `FooPart2.cs` and a new
  `partial` are not a split.
- `partial` is for source generators.

If you cannot find a clean seam, stop and say so in your report. Do not
split at a random line.

## Reuse before you build

Before you write a new interface, wrapper, or helper, find out if a package
already does the job.

**Scope**
- Applies when you are about to add an abstraction or a utility.
- Read `Directory.Packages.props` for the packages we already have.
- Search `Microsoft.Extensions.AI*` and `Microsoft.Agents.AI*` first. They are
  our base. Then search the rest.

**Prove the overlap**
- "The framework already does this" is proof only when a scratch probe
  compiles and runs against the pinned version.
- Put the probe in the scratchpad. Do not commit it.
- Name the exact type and member in your report, such as
  `DelegatingAIFunction`.
- If the probe fails, say what is missing, then write our own.
- Reason: in the 2026-08-26 framework audit, 4 of 5 "already covered" claims
  were wrong.

**A package we do not have yet**
- Propose the package before you write the code. Do not hand-roll it and
  mention the package afterwards.
- Propose one when it replaces more than ~100 lines of our code, or when the
  job is a known-hard domain: parsing, unicode, time zones, crypto, retry and
  backoff, globbing, schema validation.
- Do not install it. Give the owner a short note with:
  - the package, the exact version, and the licence
  - what it replaces, in lines and files
  - how many packages it pulls in, and any native binaries
  - the date of the last release
- AgentCore ships as NuGet packages. Every dependency lands on every consumer.
- MIT, Apache-2.0, BSD, and MS-PL are safe. A copyleft licence (GPL, AGPL,
  LGPL, MPL) is a stop. Report it and stop.
- Use a stable version. Prerelease needs the owner's OK.
- Pin the version in `Directory.Packages.props`.

**Write our own when**
- No maintained package exists.
- The library type would leak into our public API.
- Our need is under ~20 lines and the package is not.

Say which one applies in your report.

## Layout

- `src/SpiritAI/` — ASP.NET Core host. `Program.cs` wires up AgentCore
  (`AddAgentCoreHost` / `MapAgentCoreHost`) and serves the chat UI at `/chat`.
- `src/SpiritAI/config/` — agent pipelines as YAML. `spirit.yaml` is the app config
  (needs `OPENAI_API_KEY` in the environment). `example.yaml` is the annotated
  reference copied from AgentCore; treat it as documentation.
- `src/SpiritAI/Auth/` — sign-in. Verifies the Neon Auth token on `/v1`; see below.
- `src/SpiritAI/Threads/` — the thread list, as REST over AgentCore's call store; see below.
- `src/web/` — React + Vite chat frontend (assistant-ui). The MSBuild target
  `BuildClientApp` in `SpiritAI.csproj` runs `npm ci && npm run build` and Vite writes
  the bundle into `src/SpiritAI/wwwroot/chat/` (gitignored).

## Conventions

- .NET 10, nullable enabled, warnings are errors (`Directory.Build.props`).
- Config-first: agent behavior belongs in the YAML pipeline files.
