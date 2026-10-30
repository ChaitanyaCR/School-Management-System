# School Management System — Architecture & Project Plan

| | |
|---|---|
| **Status** | Draft v0.3 — backend stack changed to .NET 10 |
| **Last updated** | 2026-10-29 |
| **Related** | [01-REQUIREMENTS.md](01-REQUIREMENTS.md) |

---

## 1. Tech Stack

### Backend (.NET)

| Concern | Choice | Why |
|---|---|---|
| **Runtime** | **.NET 10 (LTS, supported to Nov 2028)**, ASP.NET Core, C# 14 | Owner's primary language. LTS release. High performance. Strong typing suits complex rules (results, attendance). |
| **API style** | Minimal APIs, with route groups registered per module | Low ceremony. Each module owns and registers its own endpoints. |
| **Data access** | **EF Core 10** + Npgsql for writes and migrations. **Dapper** for heavy read/report queries | EF for the domain model. Hand-tuned SQL for analytics such as attendance rankings. |
| **Database** | **PostgreSQL 16+** | Relational integrity, Row-Level Security for tenant isolation, JSONB for configurable schemes and templates. |
| **Background jobs** | **Hangfire** (PostgreSQL storage) | Bulk imports, PDF batches, emails, scheduled locks and purges. Retries and a dashboard, with no extra message broker. LGPL licence, free for commercial use. |
| **Cache** | .NET **HybridCache**, with Redis as an *optional* second level | A single-server pilot needs no Redis. Add it when running more than one app instance. |
| **Auth** | **ASP.NET Core Identity** + cookie auth (HttpOnly, Secure, SameSite) + antiforgery. Built-in TOTP MFA. **Custom Argon2id password hasher** | Cookies are safer than JWTs in browser storage. Identity's default PBKDF2 settings are below current OWASP guidance (NFR-SEC-03). |
| **Authorization** | Policy-based authorization with custom handlers for branch/section/subject scope | Expresses rules like "subject teacher of this section" cleanly. |
| **Validation / mapping** | FluentValidation · Mapperly (source-generated) | Free licences. Mapping is compile-time with no reflection. |
| **PDF generation** | HTML templates (**Fluid**/Liquid) rendered by **Gotenberg** (Chromium PDF service, runs as a container) | Templates are plain HTML/CSS, so they're easy to style per board or school. Liquid is sandboxed, so templates can be user-editable later. |
| **Excel / CSV** | ClosedXML · CsvHelper | Imports and exports. Permissive licences. |
| **API contract** | Microsoft.AspNetCore.OpenApi (OpenAPI 3.1) + Scalar UI | Built into .NET. The frontend TypeScript client is generated from it (orval). |
| **Logging & telemetry** | Serilog (JSON) + OpenTelemetry · Sentry SDK (or self-hosted GlitchTip) | Request/tenant IDs in every log line. Vendor-neutral. |
| **Testing** | xUnit v3 on Microsoft.Testing.Platform, **Testcontainers** (real PostgreSQL), WebApplicationFactory, Respawn, Shouldly, NSubstitute, Bogus | Tests run against real Postgres, so RLS and query filters are exercised for real. Architecture tests (reflection-based) enforce module boundaries. |
| **Code quality** | Nullable reference types, Roslyn analyzers, warnings-as-errors, `dotnet format`, `.editorconfig` | Catches problems at build time. |

> **Licensing watch.** Several popular .NET libraries moved to commercial licences in 2025: **MediatR, AutoMapper, MassTransit v9+, FluentAssertions v8+**, and QuestPDF above a revenue threshold. You plan to sell this product, so the stack avoids them: plain handler classes instead of MediatR, Mapperly instead of AutoMapper, Shouldly instead of FluentAssertions, and Gotenberg instead of QuestPDF. Every new package gets a licence check (see Definition of Done).

### Frontend & Platform

| Layer | Choice | Why |
|---|---|---|
| **Frontend** | **React 19 + TypeScript 6 + Vite** | SPA suits an authenticated dashboard app (no SEO needs). |
| **UI kit** | Tailwind CSS + shadcn/ui | Accessible, consistent components with no vendor lock-in. |
| **Data/forms** | TanStack Query, TanStack Router, React Hook Form + Zod | Server-state caching, type-safe routing, validated forms. |
| **Grids** | TanStack Table (+ custom keyboard nav for marks entry) | Needed for the spreadsheet-like marks grid. |
| **Frontend testing** | Vitest, Testing Library · **Playwright** E2E | Component tests plus full-flow tests through the real API. |
| **File storage** | Any S3-compatible storage (AWS SDK for .NET pointed at a custom endpoint): MinIO, AWS S3, GCS, Cloudflare R2, etc. | Private buckets with signed URLs. One API works on every host. |
| **Email** | Plain SMTP via MailKit: any provider (Amazon SES, Brevo, Zoho, etc.) | No vendor SDK, so switching providers is a config change. |
| **Packaging** | Docker images (Microsoft *chiseled* .NET runtime images: small, non-root) + Docker Compose, **Caddy** reverse proxy (automatic HTTPS) | The same images run on a VPS or any cloud (NFR-OPS-06). |
| **CI/CD** | GitHub Actions → container registry → deploy over SSH (VPS) or to the cloud's container service | Lint, type-check, tests, security scans, build, deploy. |
| **Backups** | pgBackRest or WAL-G → off-site S3-compatible storage | Daily full backups plus continuous WAL archiving gives point-in-time recovery and RPO ≤ 15 min on any host. |
| **Observability** | Sentry (or self-hosted GlitchTip), structured JSON logs, Uptime Kuma / Prometheus + Grafana | Vendor-neutral. Required by NFR-OPS-04. |

> **Decision (ADR-001):** .NET 10 backend, chosen over the originally proposed Django because it is the owner's primary language. Long-term maintainability by the person who owns the code outweighs Django's built-in admin. The .NET 10 LTS release is supported until Nov 2028. Upgrading to .NET 12 LTS (Nov 2027) is planned as routine maintenance.

---

## 2. Architecture

### 2.1 Style: Modular Monolith

One deployable backend split into **bounded modules** (one C# project each) with explicit contracts between them. This gives microservice-like boundaries without the operational cost. A module can be extracted later if it ever needs independent scaling (e.g. PDF generation).

```text
┌──────────────────────────── Browser (React SPA) ────────────────────────────┐
│  <org>.yourdomain.in                                                        │
└───────────────────────────────┬─────────────────────────────────────────────┘
                                │ HTTPS (JSON / OpenAPI)
┌───────────────────────────────▼─────────────────────────────────────────────┐
│ Caddy (TLS, wildcard subdomains) → ASP.NET Core API (stateless, scalable)   │
│  Pipeline: tenant resolution → auth → branch context → RLS session → audit  │
│  Modules: Tenancy · Identity · Audit · Boards · Academics · Students ·      │
│           Staff · Timetable · Attendance · Exams · Reports · Notifications  │
└──────┬───────────────────┬─────────────────────┬──────────────────┬─────────┘
       │                   │                     │                  │
┌──────▼──────┐     ┌──────▼───────┐      ┌──────▼──────┐    ┌──────▼──────┐
│ PostgreSQL  │◄────│   Hangfire   │─────►│  Gotenberg  │    │ S3 storage  │
│ RLS on;     │     │   worker     │      │ HTML → PDF  │    │ docs, PDFs  │
│ job storage │     │ (same image) │      └─────────────┘    └─────────────┘
└─────────────┘     └──────────────┘        Redis: optional cache, multi-instance
```

### 2.2 Multi-Tenancy Strategy

**Shared database, shared schema, `organisation_id` on every tenant-owned table.**

1. **Resolve** the tenant from the subdomain in middleware, and reject requests for unknown or suspended tenants. The resolver is pluggable: subdomain (default), custom domain (R4 white-label), or org code at login (fallback for hosts without wildcard DNS).
2. **App layer**: every tenant-owned entity implements `ITenantOwned`. An **EF Core named global query filter** scopes every query to the current tenant, and a `SaveChanges` interceptor stamps and validates `OrganisationId` on writes. Bypassing the filter (`IgnoreQueryFilters`) is allowed only in the platform module and is audited.
3. **DB layer (defence in depth)**: PostgreSQL RLS policies (`FORCE ROW LEVEL SECURITY`) on every tenant table check `organisation_id = current_setting('app.current_org')`. A `DbConnectionInterceptor` sets this value for each request's connection. Migrations run as the schema-owner role, while the app connects as a separate least-privilege role that cannot bypass RLS.
4. **Branch scoping** is enforced by the permission layer, not RLS, because users legitimately span branches.
5. **Tests**: a reusable test suite tries every endpoint as a user from another tenant and must get 404/403.

*Why not schema-per-tenant?* Migrations across hundreds of schemas are slow and fragile, connection pooling suffers, and platform analytics gets hard. Shared schema + RLS scales better for many small tenants.

### 2.3 Core Domain Model (simplified)

```mermaid
erDiagram
    BOARD_PROFILE ||--o{ ORGANISATION : "applied to"
    ORGANISATION ||--o{ BRANCH : has
    ORGANISATION ||--o{ USER : has
    USER ||--o{ ROLE_ASSIGNMENT : holds
    ROLE_ASSIGNMENT }o--|| ROLE : of
    ROLE_ASSIGNMENT }o--o| BRANCH : "scoped to"
    ORGANISATION ||--o{ ACADEMIC_YEAR : "shared by branches"
    ACADEMIC_YEAR ||--o{ TERM : contains
    ACADEMIC_YEAR ||--o{ CALENDAR_DAY : "holidays/events"
    ORGANISATION ||--o{ CLASS : defines
    ORGANISATION ||--o{ SUBJECT : defines
    CLASS ||--o{ CLASS_SUBJECT : teaches
    CLASS_SUBJECT }o--|| SUBJECT : is
    BRANCH ||--o{ SECTION : runs
    SECTION }o--|| CLASS : of
    SECTION }o--|| ACADEMIC_YEAR : in
    SECTION ||--o{ TEACHING_ASSIGNMENT : has
    TEACHING_ASSIGNMENT }o--|| STAFF : by
    TEACHING_ASSIGNMENT }o--|| SUBJECT : for
    STUDENT ||--o{ ENROLMENT : has
    ENROLMENT }o--|| SECTION : in
    STUDENT }o--o{ GUARDIAN : "cared by"
    SECTION ||--o{ TIMETABLE_SLOT : has
    TIMETABLE_SLOT ||--o{ ATTENDANCE_SESSION : "taken in"
    ATTENDANCE_SESSION ||--o{ ATTENDANCE_ENTRY : records
    ENROLMENT ||--o{ ATTENDANCE_ENTRY : has
    ENROLMENT ||--o{ DAILY_ATTENDANCE : "derived per day"
    ACADEMIC_YEAR ||--o{ ASSESSMENT_SCHEME : uses
    ASSESSMENT_SCHEME ||--o{ ASSESSMENT : defines
    ASSESSMENT ||--o{ MARK : records
    ENROLMENT ||--o{ MARK : earns
    ENROLMENT ||--o{ RESULT : gets
    RESULT ||--o{ REPORT_CARD : renders
```

Key design points:

- **Attendance, marks and results hang off `ENROLMENT`, not `STUDENT`**. This keeps every year's history intact through promotions and transfers.
- **Shared vs branch-owned**: the year, calendar, classes, subjects and assessment schemes belong to the organisation. Sections and everything below them belong to a branch.

### 2.5 Hosting Portability & Cost

The app is a set of Docker containers (API, Hangfire worker, Gotenberg, Caddy) plus standard backing services: **PostgreSQL, S3-compatible storage**, and optionally **Redis**. Every provider offers these, either self-hosted on a VM or as a managed service. Moving between providers means changing environment variables and migrating data. No code changes are needed.

> ⚠️ **Hostinger *shared/web* hosting will not work.** It cannot run Docker, background workers or PostgreSQL. Use a **Hostinger VPS** (KVM plan) or any equivalent VPS. (.NET also makes **Azure** a natural managed-cloud option, alongside AWS and GCP.)

| Stage | Setup | Indicative cost* |
|---|---|---|
| **Dev & demo** | Your laptop (Docker Compose) plus one small VPS for a public demo | ₹500–1,000 / month |
| **Pilot (1–3 schools, ≤ 3k students)** | 1 VPS in an India region (4 vCPU / 16 GB / 200 GB NVMe) running everything in Compose. Off-site backups to S3-compatible storage. A small separate staging VPS | ₹2,000–4,500 / month + domain ~₹1,000 / year |
| **Growth (10–50 schools)** | 2+ app servers or a managed container service, **managed PostgreSQL** (automated PITR, standby), managed Redis, object storage + CDN | ₹15,000–40,000 / month |
| **SaaS scale** | Managed Kubernetes or containers, DB read replicas, multi-zone | ₹75,000+ / month, covered by subscription revenue |

\* Rough late-2026 ranges for planning only. Validate against live pricing in Phase 8.

**Recommendation:** run the pilot on a VPS (cheapest, and fine at pilot scale), then move the database to a managed service once you have paying schools. On a self-managed VPS **you** own patching, backups and security hardening. Phase 8 covers these with scripts and runbooks.

**Product name & domain (deferred).** Each school group gets its own web address, e.g. `greenwood.<yourproduct>.in`. That needs a product name and a registered domain, but **not until Phase 8**. Until then:

- Development uses `*.localhost` (e.g. `demo.localhost:8000`), which works without DNS setup.
- The name, base domain and logo are config values (`PRODUCT_NAME`, `BASE_DOMAIN`), so picking a name later is a one-line change.
- Before buying a domain, check the name on the IP India trademark search and for `.in` / `.com` availability.

### 2.6 Board Extensibility

```text
backend/src/Modules/Boards/Profiles/
└── cbse/
    ├── profile.yaml            # metadata, version
    ├── classes.yaml            # Nursery … XII
    ├── subjects.yaml
    ├── assessment-schemes.yaml # IX–X, XI–XII, primary patterns
    ├── grading-scales.yaml     # A1–E, co-scholastic A–C
    ├── result-rules.yaml       # pass %, compartment thresholds
    ├── CbseResultRules.cs      # optional IResultRuleStrategy for non-config logic
    └── templates/              # report card & certificate HTML (Liquid)
```

- Profiles are embedded resources, loaded with YamlDotNet and validated against a schema at startup.
- Applying a profile **copies** its data into the organisation as editable records, tagged with the profile version.
- The result engine reads config first. It calls a board's `IResultRuleStrategy` (registered as a .NET *keyed service* under the board code) only for logic that config can't express (BRD-05).
- Adding CISCE or a state board later means adding a folder like this one, plus tests.

### 2.7 Period-Attendance Data Design

At design load, period attendance produces ~26M entries per year (NFR-PRF-06), so it needs deliberate design:

- One `ATTENDANCE_SESSION` row per section × date × period (who marked it, when, lock state). The entries under it hold one status per student.
- `DAILY_ATTENDANCE` is **derived** (ATT-10) and recomputed whenever a session for that day changes, so registers and report cards never scan raw period data.
- Rankings and awards (ATT-12) read from a **pre-aggregated per-enrolment summary** (periods attended/total, days present, lates), updated incrementally.
- Tables are partitioned by academic year with composite indexes. This is load-tested in Phase 5 with synthetic data at 2× design volume.

### 2.4 Repository Layout (monorepo)

```text
School-Management-System/
├── backend/
│   ├── SchoolMs.slnx
│   ├── Directory.Build.props   # shared settings: net10.0, nullable, analyzers, warnings-as-errors
│   ├── Directory.Packages.props# central package versions
│   ├── src/
│   │   ├── SchoolMs.Api/       # host: Program.cs, pipeline, module registration
│   │   │                       #   (APP_ROLE=api | worker → same image runs Hangfire worker)
│   │   ├── BuildingBlocks/
│   │   │   ├── SchoolMs.SharedKernel/    # base entities, Result type, domain events, ITenantOwned
│   │   │   └── SchoolMs.Infrastructure/  # tenant/RLS + audit interceptors, jobs, PDF, storage, email
│   │   └── Modules/
│   │       ├── Tenancy/        # organisations, branches, settings, feature flags
│   │       ├── Identity/       # users, roles, permissions, auth flows
│   │       ├── Audit/
│   │       ├── Boards/         # board registry + profiles (Profiles/cbse/ …), see §2.6
│   │       ├── Academics/      # years, terms, classes, sections, subjects, calendar
│   │       ├── Students/       # students, guardians, enrolment, promotion, certificates
│   │       ├── Staff/
│   │       ├── Timetable/
│   │       ├── Attendance/
│   │       ├── Exams/          # schemes, grading, marks, results
│   │       ├── Reports/        # report cards, dashboards, exports
│   │       └── Notifications/
│   │           # each module = SchoolMs.Modules.<X>            (Domain/ Application/ Infrastructure/ Endpoints/)
│   │           #             + SchoolMs.Modules.<X>.Contracts  (the only part other modules may reference)
│   └── tests/
│       ├── SchoolMs.ArchitectureTests/   # module-boundary rules
│       ├── SchoolMs.IntegrationTests/    # Testcontainers + WebApplicationFactory, tenant-isolation suite
│       └── SchoolMs.Modules.<X>.UnitTests/
├── frontend/
│   └── src/{app,routes,features/<module>,components,api(generated),lib}
├── infra/
│   ├── docker/                 # Dockerfiles
│   ├── compose/                # local, staging, production (VPS) compose files
│   ├── caddy/                  # reverse proxy + automatic TLS
│   └── scripts/                # provisioning, backup/restore, deploy
├── docs/                       # requirements, plan, ADRs, deploy & user guides
│   └── adr/
├── .github/workflows/
└── docker-compose.yml
```

**Module rules** (enforced by architecture tests):

- A module references other modules **only through their `.Contracts` project**. It never touches their entities or database tables.
- Each module has its own `DbContext` and its own PostgreSQL schema (e.g. `students.student`) in one shared database. Cross-module data is referenced by ID.
- Cross-module side effects use in-process domain events (e.g. `StudentEnrolled` → Attendance creates the summary row).
- Cross-module reports use read-only Dapper queries in the Reports module.
- `SchoolMs` is an internal codename for namespaces only. The product brand is configuration (§2.5).

---

## 3. Engineering Standards

### Definition of Done (every feature/PR)

- [ ] Requirement IDs referenced in the PR (e.g. `Implements SIS-06, SIS-07`)
- [ ] DB migration is backward-compatible
- [ ] Permission checks on every endpoint, plus tests for allowed and denied access
- [ ] Tenant-isolation test passes for new endpoints
- [ ] Audit logging for sensitive entities
- [ ] Unit + API tests. E2E test for critical flows
- [ ] OpenAPI schema updated and frontend client regenerated
- [ ] UI is responsive, keyboard-accessible, with loading/empty/error states
- [ ] Any new package has a licence check (permissive or LGPL only; nothing commercial)
- [ ] CI green: `dotnet build` (analyzers, warnings-as-errors), `dotnet format`, eslint, tsc, tests, security scans

### Workflow

- `main` is always deployable. One feature branch per unit of work, merged via PR.
- Conventional commits. Semantic versioning per release.
- Each phase ends with a deploy to **staging** and a demo/checklist review.

---

## 4. Phased Delivery Plan — Release 1 (MVP)

Effort is indicative, for **one full-time developer working with AI assistance**. It will vary with your availability.

| Phase | Name | Effort | Depends on |
|---|---|---|---|
| 0 | Foundations | 1–2 wk | — |
| 1 | Tenancy, Identity & Audit | 2–3 wk | 0 |
| 2 | Academic Structure, Board Profiles & Staff | 2–3 wk | 1 |
| 3 | Student Information System | 3 wk | 2 |
| 4 | Timetable | 2 wk | 2 |
| ★ | **Demo milestone → pilot school** | 1 wk | 3, 4 |
| 5 | Period Attendance & Analytics | 3 wk | 3, 4 |
| 6 | Exams & Assessment | 3–4 wk | 3 |
| 7 | Report Cards & Dashboards | 2–3 wk | 5, 6 |
| 8 | Hardening & Production Launch | 2–3 wk | all |
| | **Total** | **~21–27 wk** | |

**Pilot-school strategy.** The school will commit only after seeing the product, so the demo comes **after Phase 4**, not at the end:

- By then they can see real setup, student records and timetables on a branded demo instance, using **synthetic data only** (never real student data in dev or demo, per DPDP).
- At the demo, ask for low-commitment inputs: blank samples of their **report card, attendance register and timetable formats**, and their exam pattern. These directly shape Phases 5–7.
- From then on, give short demos at the end of each phase. UAT on production happens in Phase 8, after a data-processing agreement and parental-consent notices are in place.

### Phase 0 — Foundations ✅ (completed 2026-10-29)

**Goal:** a walking skeleton deployed end-to-end.

> **As built:** Hangfire moves to Phase 1 (first background job: emails). Gotenberg and object storage move to Phase 3 (first PDFs and uploads), where the S3-compatible store for local development will also be chosen. Code coverage reporting arrives with the first domain logic in Phase 1.

- Monorepo structure, `docker compose` for local development (PostgreSQL, MinIO, Mailpit, Gotenberg; Redis optional)
- .NET 10 solution: API host, SharedKernel, Infrastructure, one **module template** (Contracts + module + tests), central package management
- 12-factor configuration (all settings from environment variables), health checks, OpenAPI + Scalar, Serilog + OpenTelemetry
- Integration-test harness: Testcontainers PostgreSQL + WebApplicationFactory. Architecture tests for module boundaries
- React + Vite + TS app shell with routing, auth-guard placeholder, design tokens, generated API client (orval)
- Production-style Dockerfiles (multi-stage, chiseled non-root .NET images) and Caddy config
- Tooling: `.editorconfig`, analyzers, `dotnet format`, eslint, prettier, vitest, markdownlint, pre-commit hooks
- GitHub Actions CI: build → format/lint → test → build images. Secret scanning, CodeQL, `dotnet list package --vulnerable`, npm audit
- ADR-001 (stack), ADR-002 (tenancy), ADR-003 (auth), ADR-004 (board profiles), ADR-005 (hosting portability)
- **Exit:** `docker compose up` gives a running app locally at `demo.localhost`, CI is green, and "hello" renders from API → UI.

### Phase 1 — Tenancy, Identity & Audit

**Requirements:** PLT-01…09, IAM-01…12, AUD-01…03, NTF-01

Delivered in slices, each fully tested before the next:

| Slice | Scope | Status |
|---|---|---|
| 1 | EF Core per-module DbContexts, Tenancy module (organisations, branches), subdomain resolution, suspension, query filter + save guard, RLS with separate migrator/app DB roles, `migrate`/`seed-demo` commands, isolation tests | ✅ Done 2026-10-29 |
| 2 | Identity: users, Argon2id, cookie sessions + antiforgery, lockout, invites, password reset, transactional email (Hangfire + SMTP) | ✅ Done 2026-10-29 |
| 3 | Authorization: permission catalogue, built-in roles, branch-scoped assignments, policies, endpoint-level isolation harness | Next |
| 4 | Audit log (append-only), login/permission auditing | |
| 5 | Admin UI (platform: organisations; org: branches, users), branch switcher, TOTP MFA, session management | |

> **As built (slice 2):** accounts are per organisation, and sign-in accepts an email address or an Indian mobile number (SMS OTP waits for an SMS provider and DLT registration). Until slice 3, any signed-in user may send invitations; slice 3 limits this to administrators. Background jobs run inside the API process (`Jobs:RunServer`); a separate worker container is added when job volume needs it. See [ADR-0003](adr/0003-authentication.md) for the security details.

- Organisation & Branch entities, subdomain tenant-resolution middleware, `ITenantOwned` + EF named query filters, **RLS policies** and the connection interceptor
- ASP.NET Core Identity (Argon2id hasher), permission catalogue, roles, branch-scoped role assignments, policy-based authorization handlers
- Login/logout, invite, password reset, lockout, session management, TOTP MFA for admins
- Audit log infrastructure (generic, append-only)
- Platform admin console (minimal) and org admin screens for branches & users
- UI: app layout, navigation by permission, branch switcher
- Cross-tenant isolation test harness (reused by all later phases)
- **Exit:** two demo orgs exist. Users in org A cannot see anything from org B (proved by tests), and role-based menus work.

### Phase 2 — Academic Structure, Board Profiles & Staff

**Requirements:** ACD-01…12, BRD-01…05, STF-01…05

- Board registry and profile loader. **CBSE profile**: classes and subjects (grading & schemes are completed in Phase 6)
- Org level: academic years & terms, shared calendar, classes, subjects, class-subject mapping with electives, streams
- Branch level: sections, class teachers, local holidays, rooms
- Staff profiles, linking to users, bulk import, teaching assignments
- "Copy from previous year" setup wizard
- **Synthetic demo-data generator** (Bogus with its Indian locale: realistic but entirely fake data), extended in each later phase
- **Exit:** a new organisation applies the CBSE profile, sets up a shared year, and each branch completes its sections and teacher allocation through the UI.

### Phase 3 — Student Information System

**Requirements:** SIS-01…15

- Student & guardian models, sibling linking, admission-number generator
- Enrolment, roll numbers, lifecycle statuses
- CSV/Excel bulk import engine (reusable: validate → preview → commit, async)
- Search/filter/export, student profile page, document uploads
- Parental consent capture (DPDP)
- Promotion wizard, inter-branch transfer, TC & bonafide certificates (PDF pipeline set up here)
- **Exit:** a real-sized school (~1,500 students) can be imported, browsed and promoted to the next year.

### Phase 4 — Timetable

**Requirements:** TT-01…07

- Bell schedules, section timetable builder (grid UI), clash detection
- Teacher & branch views, PDF export, substitutions, versioning
- **Exit:** a full branch timetable is built with zero clashes, and teachers see their own schedule.

### ★ Demo Milestone — Pilot School

- Deploy a demo instance on a small VPS, branded for the pilot school, filled with synthetic data
- Scripted walkthrough: org setup → branches → sections → students → timetable → teacher's day view
- Collect blank format samples and exam-pattern details, and agree on a feedback cadence
- **Exit:** the school agrees to pilot, and the format samples are in hand.

### Phase 5 — Period Attendance & Analytics

**Requirements:** ATT-01…15, NFR-PRF-05…06

- Attendance sessions per timetable period, with substitution-aware marking
- Mobile-first marking UI with carry-over from the previous period. Daily mode for configured classes
- Day status derivation rule (ATT-10), lock window via scheduled task, audited edits
- Pre-aggregated attendance summaries, plus partitioning and indexes (Plan §2.7)
- Registers, defaulter reports, subject-wise attendance, dashboard tile
- **Attendance rankings & awards** with configurable criteria and tie-breaks
- Load test: per-period marking peak, plus year-long ranking queries over 2× design volume
- **Exit:** a teacher marks a period on a phone in under 20 s, the monthly register prints correctly, and the org-wide yearly ranking returns in < 3 s.

### Phase 6 — Exams & Assessment

**Requirements:** EXM-01…14, BRD-03…05 (completion)

- Org-level assessment schemes, components and weightages. Complete the CBSE profile with its scheme presets and result rules
- Grading scales (scholastic & co-scholastic), shipped through the CBSE profile
- Pluggable result-rule strategies per board
- Date sheets, marks-entry grid with keyboard navigation and special codes, Excel import
- Draft → Submitted → Verified → Locked workflow
- **Result computation engine**, isolated as pure, heavily unit-tested functions
- Result analysis reports
- **Exit:** a full term's marks are entered and locked, and computed results match a hand-verified spreadsheet for sample students, using the pilot school's actual exam pattern.

### Phase 7 — Report Cards & Dashboards

**Requirements:** RC-01…04, DSH-01…04, NTF-02

- Report-card HTML templates (term & annual) modelled on the pilot school's format, async bulk PDF generation, versioned storage
- Branch, organisation and teacher dashboards. Exports
- **Exit:** 1,000 report cards are generated in < 10 min and pass a print check, and dashboards show correct live numbers.

### Phase 8 — Hardening & Production Launch

**Requirements:** all NFRs, AUD-04…05

- Security review (OWASP ASVS L2 checklist), dependency audit, penetration test of auth & tenancy
- DPDP: privacy notice, data-subject export/erase, retention jobs, breach runbook. CERT-In log retention
- Performance tests against NFR-PRF targets, query optimisation, caching
- Finalise product name & domain. Wildcard DNS + TLS for tenant subdomains
- Provision production and staging VPS in an India region (scripted, repeatable): OS hardening, firewall, SSH keys only, automatic security updates
- pgBackRest/WAL-G backups to off-site storage, plus a **restore drill onto a fresh server** (proves RTO ≤ 4 h), monitoring & alerting
- Deployment guide for the managed-cloud path (AWS/GCP), so moving later is documented
- User guides for admin, teacher and exam coordinator. Demo tenant
- Data-processing agreement and privacy notice ready for the pilot school
- UAT with a pilot school, then fix the issues found
- **Exit:** production is live, the pilot school completes a term cycle, and there are no open critical or high issues.

---

## 5. Roadmap After R1

| Release | Scope |
|---|---|
| **R2** | Fees & payments (Razorpay/Cashfree), parent & student portal, announcements + email/SMS/WhatsApp, homework, leave requests, absence alerts, OTP login |
| **R3** | Admissions CRM, HR & payroll, staff attendance/leave, library, transport, inventory, timetable auto-generation, mobile app |
| **R4 (SaaS GA)** | Self-serve signup, subscription plans & billing, feature-gating by plan, white-labelling/custom domains, platform analytics, SSO |

---

## 6. Key Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Cross-tenant data leak | Critical — legal and reputational | App scoping + RLS + automated isolation tests on every endpoint |
| Assessment rules vary widely by board/school | High — rework | Configurable scheme engine with presets. Validate early with a real school's report card |
| Scope creep (many modules) | High — never shipping | Strict R1 scope. New ideas go to the backlog for R2+ |
| Children's data compliance (DPDP) | High — penalties | Consent, minimisation and audit built in from Phase 1/3. Legal review before launch |
| Solo-developer bandwidth | Medium | Modular monolith, scripted infrastructure, strong CI, phase exit criteria |
| Pilot school not convinced by the demo | Medium | Demo early (after Phase 4) with polished UX and realistic synthetic data. Build their formats into Phases 5–7 |
| Period-attendance data volume | Medium | Session/entry design, pre-aggregates, partitioning, load tests at 2× volume (§2.7) |
| Self-managed VPS (security/ops burden) | Medium | Hardening scripts, automatic updates, off-site backups, restore drills. Move to managed DB at growth stage |
| Hosting lock-in | Low | Only open-standard dependencies (NFR-OPS-06) |

---

## 7. Immediate Next Steps

1. ~~Confirm branch-local holidays (ACD-12)~~. Confirmed.
2. ~~Confirm the tech stack~~. Confirmed: .NET 10 + React/TS (ADR-001).
3. ~~Start Phase 0 — Foundations~~. Done. Next: **Phase 1 — Tenancy, Identity & Audit**.
