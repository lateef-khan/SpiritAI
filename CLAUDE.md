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

`AgentCore.Hosting` is published on nuget.org. `SpiritAI.csproj` pins **0.3.0**; bump
that `Version` when a newer AgentCore release adds something this app uses. Package mode
is not only a CI check — the Docker image builds this way, because the sibling source
checkout does not exist on a build machine.

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

## The widget's own door

The embeddable bubble sits on a public page, so nobody behind it is signed in and it
cannot send a token. `src/SpiritAI/PublicChat/` maps a second, unauthenticated
endpoint for it — same agent, different rules:

- `/v1/public/chat/completions`, carved back out of the guarded `/v1` prefix.
- 10 requests per caller per minute, and 4 public turns at once across everyone.
  The second is a spending cap: a per-caller limit does nothing against many callers.
- Configure under `"PublicChat"`, or `"Enabled": false` to close it.

Behind a reverse proxy the per-caller limit would see the proxy's address and act as
one shared bucket, so `src/SpiritAI/Hosting/` reads the caller out of a header
instead. It is **off by default** and `fly.toml` turns it on with
`ProxyHeaders__Enabled = "true"`.

The header is `Fly-Client-IP`, on Fly's own advice — their proxy sets it, whereas
`X-Forwarded-For` is a list a caller may already have written into. Set
`ProxyHeaders:ClientIpHeader` for a different proxy.

Why opt-in: making the header readable means emptying ASP.NET's known-proxy list,
which is only sound when the proxy is the only way in. On Fly this host is also
reachable over the org's private network and the tailnet, so the honest cost is a
bypassable rate limit from inside two networks we control.

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

## Deploying to Fly.io

One app, not two. Vite writes into `src/SpiritAI/wwwroot/chat` and the host serves it at
`/chat` beside the API at `/v1`, so a single Fly Machine carries both. `Dockerfile`
builds in package mode, so the context is this repo alone.

`fly.toml` holds only non-secret settings in `[env]`; it has no way to declare a required
secret the way a Render blueprint can. `.github/workflows/sync-fly-secrets.yml` is that
declaration — it copies `OPENAI_API_KEY`, `QDRANT_API_KEY`, `POSTGRES_CONNECTION_STRING`
and `TS_AUTHKEY` (plus the optional Grafana pair) from GitHub repository secrets into the
Fly secret store. `deploy.yml` calls it before every deploy, so there is no first-time
setup step to remember and no need for flyctl on a laptop. Note that .NET reads a nested
key from an environment variable with **two** underscores: `Auth__Neon__BaseUrl`, not
`Auth_Neon_BaseUrl`.

`VITE_NEON_AUTH_URL` is not a secret and not a runtime setting: Vite bakes it into the
browser bundle when the image is built, so it is a `[build.args]` entry in `fly.toml`. It
must name the same Neon project as `Auth__Neon__BaseUrl` or the sign-in page and the token
check will disagree.

Tailscale is not optional. The DAB MCP server in `spirit.yaml` is a tailnet address
(`100.98.168.6`), so `docker-entrypoint.sh` joins the tailnet before starting the app. A
Fly Machine is a microVM, so a real TUN device works and no SOCKS proxy or code change is
needed. Every Machine is its own node: the auth key must be **ephemeral, reusable and
pre-approved**, and the ACL on the DAB server should grant `tag:spiritai` rather than a
node name, which changes on every deploy.

To prove the Tailscale side works before spending a deploy, join the tailnet from a
throwaway container the same way `docker-entrypoint.sh` does on Fly, and open a connection
to DAB. `nc` succeeding is the real test: it proves the key is valid, the tag is accepted,
and the ACL allows it. Put the key in `.env.deploy.local` (gitignored) so it stays out of
your shell history, then:

```bash
docker run --rm --cap-add=NET_ADMIN --device=/dev/net/tun \
  --env-file .env.deploy.local --entrypoint sh tailscale/tailscale:stable -c '
    tailscaled --tun=tailscale0 --state=mem: --socket=/tmp/ts.sock >/dev/null 2>&1 &
    sleep 3
    tailscale --socket=/tmp/ts.sock up --authkey="$TS_AUTHKEY" \
      --hostname=spiritai-probe --advertise-tags=tag:spiritai \
      --accept-routes --accept-dns=false || { echo JOIN_FAIL; exit 1; }
    nc -z -w 10 100.98.168.6 5000 && echo PROBE_OK || echo ACL_FAIL'
```

`JOIN_FAIL` naming a missing tag means `tag:spiritai` is not in the ACL's `tagOwners`;
`ACL_FAIL` means it joined but the ACL does not let the tag reach DAB. The probe node is
ephemeral and removes itself.

The auth key must be **reusable** as well as ephemeral and pre-approved: every Machine
runs `tailscale up` with the same key, so a single-use key lets only the first one join.

Current `fly.toml` is tuned for development — `auto_stop_machines = "stop"`, so the app
sleeps when idle. It stops rather than suspends on purpose: a suspend restores a RAM
snapshot without re-running the entrypoint, leaving tailscaled holding a session the
coordination server has already dropped. For production set `min_machines_running = 1`
and `auto_stop_machines = "off"`.
