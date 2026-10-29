-- Database roles for LOCAL DEVELOPMENT AND TESTS ONLY (runs once, when the data volume is created).
-- Staging/production create the same roles with secret passwords from their provisioning scripts.
--
--   schoolms_migrator  owns the schemas and tables; runs migrations; BYPASSRLS for cross-tenant data fixes.
--   schoolms_app       used by the running application; data access only; row-level security always applies.

CREATE ROLE schoolms_migrator LOGIN PASSWORD 'migrator_dev' BYPASSRLS;
CREATE ROLE schoolms_app LOGIN PASSWORD 'app_dev' NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;

DO $$
BEGIN
  EXECUTE format('GRANT CONNECT, CREATE, TEMPORARY ON DATABASE %I TO schoolms_migrator', current_database());
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO schoolms_app', current_database());
END
$$;

-- Everything the migrator creates is usable (but not alterable) by the application role.
ALTER DEFAULT PRIVILEGES FOR ROLE schoolms_migrator GRANT USAGE ON SCHEMAS TO schoolms_app;
ALTER DEFAULT PRIVILEGES FOR ROLE schoolms_migrator GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO schoolms_app;
ALTER DEFAULT PRIVILEGES FOR ROLE schoolms_migrator GRANT USAGE, SELECT ON SEQUENCES TO schoolms_app;
