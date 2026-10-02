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

# Tailscale. It is installed and started with the app, but nothing in
# config/spirit.yaml uses the tailnet any more. It is removed in a later step.
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

# cloudflared opens the two SQL Servers on 127.0.0.1 for the agent's shell, through Cloudflare
# Access (cloudflared/README.md). Pinned: from 2026.6.0, `access tcp` ignores the service token
# (cloudflared issue #1673). Re-run probe P1 (docs/superpowers/plans/2026-10-01-dab-on-030.md,
# Task 1) before changing this version.
ARG CLOUDFLARED_VERSION=2026.5.1
RUN curl -fsSL "https://github.com/cloudflare/cloudflared/releases/download/${CLOUDFLARED_VERSION}/cloudflared-linux-amd64" \
      -o /usr/local/bin/cloudflared \
 && chmod +x /usr/local/bin/cloudflared \
 && cloudflared --version

# Shell tools the agent can use,
RUN apt-get update \
 && apt-get install -y --no-install-recommends python3 python3-venv unixodbc \
 && . /etc/os-release \
 && curl -fsSL https://packages.microsoft.com/keys/microsoft.asc \
      | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg \
 && curl -fsSL "https://packages.microsoft.com/config/${ID}/${VERSION_ID}/prod.list" \
      -o /etc/apt/sources.list.d/mssql-release.list \
 && apt-get update \
 && ACCEPT_EULA=Y apt-get install -y --no-install-recommends msodbcsql18 mssql-tools18 \
 && ln -s /opt/mssql-tools18/bin/sqlcmd /usr/local/bin/sqlcmd \
 && python3 -m venv /opt/venv \
 && /opt/venv/bin/pip install --no-cache-dir \
      pandas matplotlib reportlab openpyxl pyodbc \
 && rm -rf /var/lib/apt/lists/*
ENV PATH="/opt/venv/bin:${PATH}"

WORKDIR /app
COPY --from=build /app/publish ./
COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

EXPOSE 8080
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
CMD ["dotnet", "/app/SpiritAI.dll"]
