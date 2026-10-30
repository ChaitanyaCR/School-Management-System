# School Management System

A multi-tenant School Management System for school groups and standalone schools in India, built to be offered as SaaS.

- **Requirements:** [docs/01-REQUIREMENTS.md](docs/01-REQUIREMENTS.md)
- **Architecture & delivery plan:** [docs/02-PROJECT-PLAN.md](docs/02-PROJECT-PLAN.md)
- **Architecture decisions:** [docs/adr/](docs/adr/README.md)

**Status:** Phase 0 (Foundations) complete. Phase 1 (Tenancy, Identity & Audit) in progress: slices 1 (tenancy and data isolation) and 2 (sign-in, invitations, password reset) done.

## Tech stack

| Layer | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core Minimal APIs, EF Core 10, PostgreSQL 17 |
| Frontend | React 19, TypeScript, Vite, TanStack Query/Router, Tailwind CSS, shadcn/ui |
| Testing | xUnit v3 + Testcontainers (real PostgreSQL), Vitest + Testing Library |
| Runtime | Docker, Caddy (reverse proxy, automatic HTTPS) |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Node.js 22.12 or later (CI and the Docker image use Node 26 LTS)
- Docker with Docker Compose
- `make` (optional; each target is a plain command you can run yourself)

## Getting started

```bash
make setup   # git hooks, restore .NET and npm packages (once per clone)
```

### Option A: full stack in Docker

```bash
make up      # http://demo.localhost:8080
make down
```

### Option B: native development with hot reload (the usual way to work)

```bash
make db      # PostgreSQL (port 5433) + Mailpit in Docker, migrations, demo organisation
make api     # API on http://localhost:5080, API docs on http://localhost:5080/api/docs
make web     # Web app on http://demo.localhost:5173 (separate terminal)
```

`*.localhost` addresses resolve to your machine automatically in modern browsers. The subdomain selects the organisation (tenant): `demo.localhost` is the seeded *Demo School Group*, while plain `localhost` is the platform itself.

`make db-reset` deletes the local database and rebuilds it. You need it after changing `infra/postgres/init/`.

### Signing in locally

The demo organisation has two synthetic users (development only; seeding is refused in production):

| User | Email | Password |
|---|---|---|
| Asha Verma | `admin@demo.test` | `schoolms-demo-2026` |
| Rahul Nair | `teacher@demo.test` | `schoolms-demo-2026` |

Invitation and password reset emails are caught by Mailpit at <http://localhost:8025>. Until permissions arrive (Phase 1 slice 3), any signed-in user can invite others.

| Service | URL |
|---|---|
| Web app (native) | <http://demo.localhost:5173> |
| Web app (Docker) | <http://demo.localhost:8080> |
| API reference (development only) | <http://localhost:5080/api/docs> |
| Mailpit (captured emails) | <http://localhost:8025> |
| Health | `/health/live`, `/health/ready` |

## Configuration

Settings come from `appsettings.json`, overridden per environment by environment variables (`Section__Key`). Real environments must set:

| Setting | Example | Notes |
|---|---|---|
| `ConnectionStrings__Database` | `Host=…;Username=schoolms_app;…` | Application role (RLS applies) |
| `ConnectionStrings__Migrations` | `Host=…;Username=schoolms_migrator;…` | Only for the `migrate` command |
| `App__BaseDomain` | `example.in` | Schools are served from `{school}.example.in` |
| `App__PublicOrigin` | `https://example.in` | Used to build links in emails |
| `Smtp__Host`, `Smtp__Port`, `Smtp__Security`, `Smtp__Username`, `Smtp__Password`, `Smtp__FromAddress` | `email-smtp.ap-south-1.amazonaws.com`, `587`, `StartTls` | Any SMTP provider |

Optional: `Identity__CheckBreachedPasswords` (default `true`), `Jobs__RunServer` (default `true`), `Jobs__WorkerCount` (default `2`). `Security__RequireSecureCookies` must stay `true` (the default) anywhere served over HTTPS.

## Common tasks

```bash
make test                  # all tests (backend integration tests need Docker running)
make check                 # formatting, lint, type-check (same as CI)
make format                # auto-format backend and frontend
make openapi               # after changing an endpoint: regenerate OpenAPI + frontend client
make module NAME=Students  # scaffold a new backend module
make migration MODULE=Tenancy NAME=AddBranchAddress  # add an EF Core migration
```

## Repository layout

```text
backend/            .NET solution (SchoolMs.slnx)
  src/SchoolMs.Api/         host: startup, pipeline, module registration
  src/BuildingBlocks/       SharedKernel (domain primitives), Infrastructure (cross-cutting)
  src/Modules/              business modules (added from Phase 1)
  tests/                    unit, architecture and integration tests
  openapi/                  generated OpenAPI document (committed)
  templates/module/         `dotnet new` template used by `make module`
frontend/           React app
  src/routes/               file-based routes (TanStack Router)
  src/features/             feature components
  src/components/ui/        shadcn/ui components
  src/api/generated/        typed API client generated from the OpenAPI document
infra/              Dockerfiles and Caddy configuration
docs/               requirements, plan, ADRs
```

## Conventions

- **Branches and PRs:** `main` is always deployable. Work happens on a branch and is merged via PR with CI green.
- **Requirement IDs:** reference them in PRs and commits (e.g. `Implements SIS-06`).
- **Definition of Done:** see [plan §3](docs/02-PROJECT-PLAN.md#3-engineering-standards).
- **Packages:** only permissive or LGPL licences. Versions are managed centrally in `backend/Directory.Packages.props`.
- **Data:** never use real student data in development or demos. Use the synthetic data generator (from Phase 2).
- **Tenant data:** every tenant-owned entity implements `ITenantOwned`, and its migration calls `EnableTenantRowLevelSecurity` (a test fails otherwise). The app connects as `schoolms_app`, which can never bypass row-level security. Migrations run as `schoolms_migrator`. See [ADR-0002](docs/adr/0002-multi-tenancy.md).
