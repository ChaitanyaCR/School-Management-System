# ADR-0005: Provider-agnostic hosting on Docker and open standards

- **Status:** Accepted
- **Date:** 2026-10-29

## Context

The hosting provider and budget are undecided. The product must be able to run on a single low-cost VPS (e.g. Hostinger VPS) for a pilot and move to managed cloud (AWS, GCP, Azure) as it grows, with data hosted in India.

## Decision

- Everything ships as Docker images and runs with Docker Compose. The API image is a chiseled .NET runtime with ICU and tzdata. The web image is Caddy, serving the SPA and reverse-proxying the API with automatic TLS.
- The only backing services are open standards: PostgreSQL, S3-compatible object storage, SMTP, and optionally Redis. No proprietary cloud API is used in the core path.
- All configuration comes from environment variables (12-factor). Product name and base domain are configuration.
- Background jobs (Hangfire) store their state in PostgreSQL, so no extra message broker is needed.

## Consequences

- Moving providers means changing environment variables and migrating data, not code.
- On a self-managed VPS we own patching, backups and hardening. Phase 8 delivers scripts and runbooks for these.
- Some managed-service conveniences (e.g. vendor queues) are deliberately left unused.
