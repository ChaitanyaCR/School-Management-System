# Architecture Decision Records

Short records of significant decisions: the context, what was decided, and the consequences. An accepted ADR is not edited; a later ADR supersedes it.

| # | Decision | Status |
|---|---|---|
| [0001](0001-backend-stack.md) | .NET 10 backend, React + TypeScript frontend, PostgreSQL | Accepted |
| [0002](0002-multi-tenancy.md) | Shared-schema multi-tenancy with EF Core query filters and PostgreSQL RLS | Accepted |
| [0003](0003-authentication.md) | Cookie-based sessions with ASP.NET Core Identity | Accepted |
| [0004](0004-board-profiles.md) | Board-specific rules as versioned data packs | Accepted |
| [0005](0005-hosting-portability.md) | Provider-agnostic hosting on Docker and open standards | Accepted |

New ADRs copy the structure of an existing one: **Context → Decision → Consequences**.
