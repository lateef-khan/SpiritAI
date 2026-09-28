-- Makes one app's role and database, or brings them up to date. Safe to run again.
-- `just setup` runs it once per app, with psql variables role, password, and database.

SELECT format('CREATE ROLE %I LOGIN', :'role')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = :'role') \gexec

-- Every run, so a new password in secrets/prod.env takes effect.
ALTER ROLE :"role" WITH LOGIN PASSWORD :'password';

SELECT format('CREATE DATABASE %I OWNER %I', :'database', :'role')
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = :'database') \gexec

-- Only the owner connects, so one app cannot open the other's database.
REVOKE ALL ON DATABASE :"database" FROM PUBLIC;
