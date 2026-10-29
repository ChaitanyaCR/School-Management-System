# ADR-0003: Cookie-based sessions with ASP.NET Core Identity

- **Status:** Accepted
- **Date:** 2026-10-29

## Context

Release 1 users are school staff using a browser. The web app and API are served from the same origin (Caddy in production, the Vite proxy in development).

## Decision

- ASP.NET Core Identity for users, password reset, lockout and TOTP MFA.
- **Session cookie** (HttpOnly, Secure, SameSite=Lax) plus antiforgery tokens for state-changing requests. Tokens are never stored in browser storage.
- **Argon2id** password hashing through a custom `IPasswordHasher`, because Identity's default PBKDF2 settings are below current OWASP guidance.
- Authorization is policy-based, with handlers for organisation, branch, section and subject scope.
- Token-based auth (for mobile apps) is deferred to Release 2 and will get its own ADR.

## Consequences

- No XSS-exposed tokens, and no CORS setup needed (same origin).
- Antiforgery must be wired into the frontend HTTP client in Phase 1.
- A future mobile app needs a separate auth flow.
