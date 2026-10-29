# ADR-0002: Shared-schema multi-tenancy with defence in depth

- **Status:** Accepted
- **Date:** 2026-10-29

## Context

Each customer organisation (a school group) is a tenant. A cross-tenant data leak of children's data would be critical (DPDP Act). The platform must scale to hundreds of small tenants on modest infrastructure.

## Decision

- One database and one schema set. Every tenant-owned table has an `organisation_id` column, and the entity implements `ITenantOwned`.
- **Tenant resolution:** from the subdomain (`{org}.{BaseDomain}`) by default. The resolver is pluggable, so custom domains or an org code at login can be added later.
- **Application layer:** an EF Core named global query filter scopes every query to the current tenant. A `SaveChanges` interceptor stamps and validates `OrganisationId`. `IgnoreQueryFilters` is allowed only in platform code and is audited.
- **Database layer:** PostgreSQL Row-Level Security with `FORCE ROW LEVEL SECURITY` on every tenant table, matched against `current_setting('app.current_org')`. A connection interceptor sets this per request. Migrations run as the schema owner, while the app connects as a separate least-privilege role that can't bypass RLS.
- Branch-level access is enforced by authorization policies, not RLS, because users can legitimately work in several branches.
- A reusable integration-test suite calls every endpoint as another tenant's user and expects 404/403.

## Consequences

- Two independent safeguards: a bug in one is caught by the other.
- Migrations and connection pooling stay simple compared with schema-per-tenant designs.
- Each tenant table needs an RLS policy, so the module template and code review must include it.
- Platform-wide analytics need an explicit, audited bypass.
