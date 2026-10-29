# School Management System

A multi-tenant School Management System for school groups and standalone schools in India, built to be offered as SaaS.

- **Requirements:** [docs/01-REQUIREMENTS.md](docs/01-REQUIREMENTS.md)
- **Architecture & delivery plan:** [docs/02-PROJECT-PLAN.md](docs/02-PROJECT-PLAN.md)
- **Architecture decisions:** [docs/adr/](docs/adr/README.md)

**Status:** Phase 0 (Foundations) complete. Phase 1 (Tenancy, Identity & Audit) is next.

## Tech stack

| Layer | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core Minimal APIs, EF Core 10, PostgreSQL 17 |
| Frontend | React 19, TypeScript, Vite, TanStack Query/Router, Tailwind CSS, shadcn/ui |
| Testing | xUnit v3 + Testcontainers (real PostgreSQL), Vitest + Testing Library |
| Runtime | Docker, Caddy (reverse proxy, automatic HTTPS) |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Node.js 22.12 or later
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
make deps    # PostgreSQL (port 5433) and Mailpit in Docker
make api     # API on http://localhost:5080, API docs on http://localhost:5080/api/docs
make web     # Web app on http://demo.localhost:5173 (separate terminal)
```

`*.localhost` addresses resolve to your machine automatically in modern browsers. The subdomain (`demo`) will select the tenant once Phase 1 is in place.

| Service | URL |
|---|---|
| Web app (native) | <http://demo.localhost:5173> |
| Web app (Docker) | <http://demo.localhost:8080> |
| API reference (development only) | <http://localhost:5080/api/docs> |
| Mailpit (captured emails) | <http://localhost:8025> |
| Health | `/health/live`, `/health/ready` |

## Common tasks

```bash
make test                  # all tests (backend integration tests need Docker running)
make check                 # formatting, lint, type-check (same as CI)
make format                # auto-format backend and frontend
make openapi               # after changing an endpoint: regenerate OpenAPI + frontend client
make module NAME=Students  # scaffold a new backend module
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
