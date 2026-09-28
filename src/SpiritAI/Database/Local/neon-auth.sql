-- A local copy of the one Neon Auth table Spirit points a foreign key at. Neon creates the real
-- one; the throwaway database (`just spirit db-up`) and the database tests get this copy before
-- the migrations run. Columns match neon_auth."user" on Neon as of 2026-09-27.
CREATE SCHEMA IF NOT EXISTS neon_auth;

CREATE TABLE IF NOT EXISTS neon_auth."user" (
    id              uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    name            text        NOT NULL,
    email           text        NOT NULL UNIQUE,
    "emailVerified" boolean     NOT NULL,
    image           text,
    "createdAt"     timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt"     timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    role            text,
    banned          boolean,
    "banReason"     text,
    "banExpires"    timestamptz
);
