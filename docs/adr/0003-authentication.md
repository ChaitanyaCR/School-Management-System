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

## Implementation (Phase 1, slice 2)

| Concern | How it works |
|---|---|
| Accounts | One account per person per organisation. The users table is tenant-owned (query filter and RLS), so sign-in, tokens and sessions only ever see the current organisation's users. Sign-in accepts an email address or an Indian mobile number. |
| Password storage | Argon2id through libsodium (NSec), m = 19 MiB, t = 2, p = 1 (OWASP). Standard PHC strings, re-hashed automatically if the parameters are raised later. |
| Password rules | NIST SP 800-63B: 10–128 characters, no composition rules, must not contain the email address, must not appear in Have I Been Pwned (k-anonymity range API: only 5 hex characters of the SHA-1 hash leave the server). If the service is unreachable, the check is skipped. It can be switched off with `Identity:CheckBreachedPasswords`. |
| Lockout and throttling | 5 failed attempts lock the account for 15 minutes. Per organisation and client IP: 20 sign-in attempts per minute, 5 reset emails per 10 minutes. Unknown accounts, wrong passwords and inactive accounts get the same answer, and take the same time. |
| Session | `__Host-session` cookie: HttpOnly, Secure, SameSite=Lax, 12 hours sliding (14 days with "keep me signed in"). The session carries its organisation, and a session presented on another organisation's address is rejected. The security stamp is re-checked every 5 minutes, so a password reset ends other sessions within that time. |
| CSRF | ASP.NET Core antiforgery: the SPA copies the `XSRF-TOKEN` cookie into the `X-XSRF-TOKEN` header on every state-changing `/api` request. The token is bound to the user and re-issued on sign-in and sign-out. |
| Authorization default | The `/api/v1` route group requires a signed-in user. Endpoints that allow anonymous access are listed in `EndpointSecurityTests`, so adding one is a reviewed change. |
| Invitation and reset links | ASP.NET Core Identity data-protection tokens. Invitations last 72 hours, resets 1 hour. Both stop working after use or when a newer link is sent, because they embed the security stamp. The token is in the URL fragment (`#uid=…&token=…`), which browsers never send to servers or put in Referer headers, and the page removes it from the address bar. The token is generated inside the email job, so it is never stored in the job queue. |
| Key ring | Data Protection keys live in `identity.data_protection_keys`, shared by all instances and kept across deploys. **Before production:** encrypt the keys at rest (e.g. `ProtectKeysWithCertificate`), tracked for Phase 8 hardening. |
| Email | Hangfire job (PostgreSQL storage) renders Liquid templates and sends through any SMTP provider (`Smtp:*`). Logs carry user ids, never email addresses or phone numbers. |
