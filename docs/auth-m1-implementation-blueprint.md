# M1 Auth Implementation Blueprint

This note turns the auth decision into a concrete M1 plan.

Use this file for:

- the chosen M1 auth shape
- endpoint and error contracts
- persistence and configuration boundaries
- security defaults
- delivery and validation order

Use [`auth-mvp-decision.md`](auth-mvp-decision.md) for the higher-level option comparison and roadmap
reasoning.

## Table Of Contents

- [At A Glance](#at-a-glance)
- [Visual Map](#visual-map)
- [M1 Scope](#m1-scope)
- [Architecture Decisions](#architecture-decisions)
- [API Contract](#api-contract)
- [Security Defaults](#security-defaults)
- [Persistence And Naming](#persistence-and-naming)
- [Configuration And Startup](#configuration-and-startup)
- [Delivery Order](#delivery-order)
- [Validation](#validation)
- [Status](#status)

## At A Glance

| Area | M1 decision |
| --- | --- |
| Login method | Email + password |
| Browser auth transport | Secure `HttpOnly` cookie |
| Browser token handling | None |
| Web flow style | Backend-managed web session, BFF-aligned but not full proxy-BFF |
| Auth foundation | ASP.NET Core Identity + OpenIddict |
| Native/mobile in M1 | No |
| External providers in M1 | No |
| Roles in M1 | No |
| Canonical user identity | Auth DB `UserId` used directly everywhere |
| Auth persistence | Separate auth database on the same PostgreSQL server |

## Visual Map

```text
Browser
  -> GET /auth/antiforgery
  -> POST /auth/sign-up or POST /auth/sign-in
  -> backend verifies user with Identity
  -> backend sets secure auth cookie
  -> browser calls GET /auth/me on startup
  -> browser calls normal protected app endpoints

Behind the scenes
  -> Identity owns local users and passwords
  -> OpenIddict is present as the future token-capable foundation
  -> app data stays in app DB
  -> auth data stays in auth DB
```

## M1 Scope

### Included

- sign-up with email and password
- sign-in with email and password
- sign-out
- `GET /auth/antiforgery`
- `GET /auth/me`
- authenticated API access for user-scoped features
- ASP.NET Core Identity for local account management
- OpenIddict introduced in a narrow foundation-first shape
- end-to-end validation of the real web flow

### Not included

- Google or other social login
- Authorization Code + PKCE browser flow
- refresh tokens
- native/mobile client delivery
- account linking
- password reset and recovery
- email verification
- multi-factor authentication
- role-based authorization
- advanced device/session management

## Architecture Decisions

### Web auth flow

Use a backend-managed secure web session that is BFF-aligned, but not a full proxy-style BFF.

What that means:

- the browser signs up or signs in through backend auth endpoints
- the backend establishes authenticated state through a secure `HttpOnly` cookie
- the frontend restores auth state through `GET /auth/me`
- business endpoints remain normal app endpoints in M1
- browser JavaScript does not manage access tokens directly

### Why not the other main web options

| Option | Why it was not chosen for M1 |
| --- | --- |
| Full BFF / proxy style | Stronger boundary, but more architecture and implementation weight than the app needs right now |
| SPA token handling in browser | More browser auth complexity and worse fit for the current web-first product |

### Identity and OpenIddict split

| Concern | M1 owner |
| --- | --- |
| Local users, passwords, sign-in, sign-out | ASP.NET Core Identity |
| Future OAuth/OIDC-capable backend foundation | OpenIddict |
| Current browser auth state | Identity cookie auth |

### OpenIddict scope in M1

Use a narrow, foundation-first OpenIddict setup.

| Area | M1 decision |
| --- | --- |
| OpenIddict Core | Included |
| OpenIddict Server | Included in a minimal foundation configuration |
| OpenIddict Validation | Not required unless a real bearer-token API surface appears |
| Discovery/token endpoints for external clients | Not part of the accepted M1 user flow |
| Authorization Code + PKCE | Not implemented in M1 |
| Refresh tokens | Not implemented in M1 |
| Client registration | Not required for M1 web delivery |

### User boundary

Use one canonical user identity in the auth database.

| Decision | M1 direction |
| --- | --- |
| Canonical identity | Auth DB owns `UserId` |
| App-side user table | Do not create one by default |
| App ownership fields | Store the auth `UserId` directly |
| Mapping table | No |
| Canonical ID type | `Guid` / UUID |

### Sign-up bootstrap

Sign-up should write only to the auth database.

The app database should create user-scoped rows lazily when the user actually starts using product features.

### Authorization model

M1 authorization is intentionally simple:

- unauthenticated user
- authenticated user

No roles in M1.

## API Contract

### Endpoint set

| Endpoint | Purpose |
| --- | --- |
| `GET /auth/antiforgery` | Issue the antiforgery token needed for SPA write requests |
| `POST /auth/sign-up` | Create account and sign in immediately |
| `POST /auth/sign-in` | Sign in with email and password |
| `POST /auth/sign-out` | End the current authenticated session |
| `GET /auth/me` | Return the current authenticated user |

### Success contracts

| Endpoint | Request | Success response | Notes |
| --- | --- | --- | --- |
| `GET /auth/antiforgery` | no body | lightweight success with request token | emits the antiforgery cookie and returns the request token the SPA must echo back |
| `POST /auth/sign-up` | `email`, `password` | current user summary | M1 auto-signs-in after successful sign-up |
| `POST /auth/sign-in` | `email`, `password` | current user summary | establishes authenticated web session |
| `POST /auth/sign-out` | no body | `204 No Content` | clears auth state |
| `GET /auth/me` | no body | current user summary | returns `401` when signed out |

### Current-user response shape

Keep the M1 user summary intentionally small.

| Field | Purpose |
| --- | --- |
| `userId` | stable local user identifier |
| `email` | account email |

Do not require or expose `displayName` as core auth data in M1.

### Frontend auth-state rules

| Situation | Frontend behavior |
| --- | --- |
| First app load before browser write requests | call `GET /auth/antiforgery` so the SPA can send the antiforgery header on unsafe requests |
| App startup | call `GET /auth/me` |
| `GET /auth/me` returns `200` | hydrate signed-in state |
| `GET /auth/me` returns `401` | treat as normal signed-out state |
| `POST /auth/sign-in` succeeds | update UI from returned user summary |
| `POST /auth/sign-up` succeeds | update UI from returned user summary |
| `POST /auth/sign-out` succeeds | clear local signed-in state |

### Error contract

Use plain HTTP status codes for auth outcomes:

| Status | Use when |
| --- | --- |
| `400` | request payload is invalid; rely on the standard ASP.NET Core validation response |
| `401` | sign-in credentials are wrong or a protected endpoint is called without auth |
| `409` | sign-up email already exists |
| `423` | lockout is active |

Rules:

- invalid credentials must use the same `401` response whether the email exists or not
- the frontend should treat `401` from `GET /auth/me` as a normal signed-out state

## Security Defaults

### Password and sign-in protection

| Area | M1 rule |
| --- | --- |
| Password storage | ASP.NET Core Identity hashing only |
| Minimum password length | 8 characters |
| Required character classes | none beyond normal printable input |
| Email normalization | trim + case-insensitive comparison |
| Invalid credential message | generic response |
| Failed sign-in tracking | enabled |
| Lockout threshold | 5 failed attempts |
| Lockout duration | 15 minutes |
| Failure reset | on successful sign-in |
| Sign-up throttling | yes |
| Sign-in throttling | yes |

### Web session policy

| Setting | M1 decision |
| --- | --- |
| Session lifetime | 8 hours |
| Sliding expiration | enabled |
| Remember me | not included in M1 |
| Long-lived persistent login | not included in M1 |
| Sign-out behavior | immediate invalidation of the current session |

### Cookie baseline

| Setting | Local development | Staging and production |
| --- | --- | --- |
| Auth cookie name | `langoose.auth` | `langoose.auth` |
| Antiforgery cookie name | `langoose.csrf` | `langoose.csrf` |
| `HttpOnly` auth cookie | `true` | `true` |
| `SameSite` | `Lax` | `Lax` |
| `SecurePolicy` | `SameAsRequest` if local HTTP is still used | `Always` |
| `Domain` | unset, host-only | unset by default, widen only deliberately |
| `Path` | `/` | `/` |
| `IsEssential` | `true` | `true` |

### CSRF posture

Cookie auth means M1 must use real antiforgery protection for browser write requests.

| Area | M1 decision |
| --- | --- |
| Browser reads such as `GET /auth/me` | no antiforgery token required |
| Browser writes such as sign-up, sign-in, sign-out, and later authenticated writes | require antiforgery validation |
| Antiforgery transport | backend emits a cookie token plus a request token, and the SPA echoes the request token back in a request header |
| Auth cookie | stays `HttpOnly` |
| Antiforgery token cookie | managed by the browser; the SPA uses the returned request token for the header |
| CORS/origin policy | strict and same-site by default |

Recommended bootstrap sequence:

1. SPA calls `GET /auth/antiforgery`
2. backend emits the `langoose.csrf` cookie token and returns a request token payload
3. SPA stores that request token and sends it in a request header on unsafe requests
4. backend validates the antiforgery token before accepting browser write requests

## Persistence And Naming

### Persistence boundary

Use:

- one PostgreSQL server
- one app database
- one separate auth database
- separate migration streams

### Code and migration layout

| Area | Recommendation |
| --- | --- |
| App-domain database | keep existing app persistence in `Langoose.Data` |
| Auth database | add a separate `AuthDbContext` in `Langoose.Auth.Data` |
| App migrations | keep app migrations in `Migrations` inside `Langoose.Data` |
| Auth migrations | keep auth migrations in `Migrations` inside `Langoose.Auth.Data` |
| Design-time factory | add a separate auth design-time factory |

This keeps one data project, two databases, two `DbContext` types, and two migration streams.

### Concrete names

| Area | M1 name |
| --- | --- |
| App database | `langoose_app` |
| Auth database | `langoose_auth` |
| App connection string key | `ConnectionStrings:AppDatabase` |
| Auth connection string key | `ConnectionStrings:AuthDatabase` |
| Auth `DbContext` type | `AuthDbContext` |
| Auth migrations folder | `Migrations` in `Langoose.Auth.Data` |

## Configuration And Startup

### Configuration shape

| Section | Purpose |
| --- | --- |
| `ConnectionStrings:AppDatabase` | app-domain data |
| `ConnectionStrings:AuthDatabase` | Identity and OpenIddict data |
| `Authentication` | cookie and sign-in settings |
| `OpenIddict` | server-foundation settings |

### Startup wiring

| Area | Recommendation |
| --- | --- |
| `Program.cs` | owns runtime auth wiring |
| App DB registration | uses `AppDbContext` + `ConnectionStrings:AppDatabase` |
| Auth DB registration | uses `AuthDbContext` + `ConnectionStrings:AuthDatabase` |
| Identity registration | configured against `AuthDbContext` |
| OpenIddict registration | configured against `AuthDbContext` |

### Migration policy

| Environment | Policy |
| --- | --- |
| Local development | auto-apply both auth DB and app DB migrations at startup |
| Local Docker | auto-apply both auth DB and app DB migrations at startup |
| Staging | explicit migration step, not blind normal-startup migration |
| Production later | same explicit policy as staging |

Migration order:

1. auth database
2. app database
3. application startup

## Delivery Order

1. Add ASP.NET Core Identity and auth persistence wiring.
2. Add the separate auth database and auth migration stream.
3. Add `AuthDbContext`, design-time factory, and auth `Migrations`.
4. Add the narrow OpenIddict foundation.
5. Implement `sign-up`, `sign-in`, `sign-out`, and `me`.
6. Move protected API endpoints to the real auth substrate.
7. Replace the frontend placeholder auth UI and bootstrap logic.
8. Add tests.
9. Validate the full flow in the local stack and staging-like conditions.

## Validation

### Acceptance checklist

M1 is not done until these paths work:

- sign-up succeeds
- sign-in succeeds
- invalid credentials fail cleanly
- authenticated users can access protected flows
- signed-out users cannot access protected flows
- sign-out works
- app startup restores the correct signed-in or signed-out state

### Test split

| Layer | What it should cover |
| --- | --- |
| Backend integration tests | auth contract, invalid credentials, lockout, unauthorized access, antiforgery enforcement |
| Frontend flow checks | bootstrap, sign-in, sign-up, sign-out, `401` handling from `GET /auth/me` |
| Docker end-to-end validation | both databases, migrations, cookies, CSRF-protected writes, full local auth flow |

### Minimum M1 bar

1. backend integration tests cover the auth contract and security-critical edge cases
2. frontend build stays green and the auth entry flow is checked in the browser
3. the Docker-based local stack proves the real auth flow works end to end

## Status

The major auth-direction decisions for M1 are now pinned down.

The remaining work is implementation detail, not strategy uncertainty.
