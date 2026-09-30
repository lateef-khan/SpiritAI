# The pgvector image plus pgBackRest, which production uses for backups to S3. Dev uses the same
# image with backups off. See README.md, "Backups".
FROM pgvector/pgvector:pg16

# Pinned: the PGDG repository drops old versions, so a failed build here means a new release.
# Read its release notes, then change the version. ca-certificates is only a recommend of
# pgbackrest, and without it pgBackRest cannot verify the S3 host's HTTPS certificate.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends pgbackrest=2.59.1-1.pgdg12+1 ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY schedule.sh /usr/local/bin/
