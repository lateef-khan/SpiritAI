# syntax=docker/dockerfile:1
#
# Build context is this repo. AgentCore comes from the AgentCore.Hosting NuGet
# package (UseLocalAgentCore=false), so the sibling source checkout is NOT needed
# here -- and must not be, since it does not exist on a build machine.
#
#   fly deploy
#
# ---------------------------------------------------------------- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Node is required, not optional: the BuildClientApp target in SpiritAI.csproj
# runs `npm ci && npm run build` and Vite writes the bundle into wwwroot/chat.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates gnupg \
 && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
 && apt-get install -y --no-install-recommends nodejs \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /src

# Restore on its own layer so a source-only edit does not re-download NuGet.
COPY Directory.Build.props ./
COPY src/SpiritAI/SpiritAI.csproj src/SpiritAI/
RUN dotnet restore src/SpiritAI/SpiritAI.csproj -p:UseLocalAgentCore=false

COPY . .

# Vite bakes every VITE_ variable into the bundle at build time, so this cannot
# be a Fly secret -- by the time the container runs, the JavaScript is already
# written. It is public by design (see src/web/.env.example): the browser posts
# to this URL, and it carries no credential. Supplied from fly.toml [build.args].
ARG VITE_NEON_AUTH_URL=""
ENV VITE_NEON_AUTH_URL=${VITE_NEON_AUTH_URL}
RUN test -n "$VITE_NEON_AUTH_URL" \
 || { echo "ERROR: VITE_NEON_AUTH_URL build arg is empty -- the sign-in page would have no auth URL. Set it in fly.toml [build.args]." >&2; exit 1; }

# The csproj, never SpiritAI.slnx: the solution also lists the local AgentCore
# projects, which are not in this image.
RUN dotnet publish src/SpiritAI/SpiritAI.csproj \
      -c Release \
      -o /app/publish \
      -p:UseLocalAgentCore=false

# ------------------------------------------------------------- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Tailscale. The DAB MCP server in config/spirit.yaml lives at
# http://100.98.168.6:5000/mcp, a tailnet address that is unreachable from the
# public internet, so this machine has to join the tailnet itself.
#
# ${ID} matters: the aspnet:10.0 image is UBUNTU (noble), not Debian, and
# pkgs.tailscale.com/stable/debian/noble.noarmor.gpg is a 404. Reading ID from
# os-release rather than hard-coding a distro keeps this working if the base
# image changes underneath us.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates gnupg iproute2 iptables \
 && . /etc/os-release \
 && curl -fsSL "https://pkgs.tailscale.com/stable/${ID}/${VERSION_CODENAME}.noarmor.gpg" \
      -o /usr/share/keyrings/tailscale-archive-keyring.gpg \
 && curl -fsSL "https://pkgs.tailscale.com/stable/${ID}/${VERSION_CODENAME}.tailscale-keyring.list" \
      -o /etc/apt/sources.list.d/tailscale.list \
 && apt-get update \
 && apt-get install -y --no-install-recommends tailscale \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish ./
COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

EXPOSE 8080
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
CMD ["dotnet", "/app/SpiritAI.dll"]
