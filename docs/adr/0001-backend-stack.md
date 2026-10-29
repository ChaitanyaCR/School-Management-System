# ADR-0001: .NET 10 backend, React + TypeScript frontend, PostgreSQL

- **Status:** Accepted
- **Date:** 2026-10-29

## Context

The system is a long-lived, multi-tenant SaaS product maintained mainly by its owner, an experienced .NET developer. Django was first proposed for its built-in admin and fast CRUD development.

## Decision

- **Backend:** .NET 10 (LTS, supported to Nov 2028), ASP.NET Core Minimal APIs, EF Core 10 + Npgsql, Dapper for heavy reads.
- **Frontend:** React 19 + TypeScript (Vite), TanStack Query/Router, Tailwind CSS + shadcn/ui.
- **Database:** PostgreSQL 17.
- **Architecture:** modular monolith, one project per business module, with modules talking to each other only through `.Contracts` projects.
- Only permissive (MIT/Apache/BSD) or LGPL packages. This excludes MediatR, AutoMapper, MassTransit v9+, FluentAssertions v8+ and QuestPDF, which moved to commercial licences.

## Consequences

- The owner's existing expertise in the language outweighs Django's built-in admin, so we build our own admin screens.
- Strong typing and compile-time analysis (warnings as errors) suit complex rules such as result computation.
- Plan an upgrade to .NET 12 LTS (Nov 2027) before .NET 10 support ends.
- The API contract (OpenAPI) is generated at build time and drives the typed frontend client, so the frontend and backend can't drift apart silently.
