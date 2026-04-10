---
name: langoose-auth-hosting
description: Work on Langoose auth, cookie and antiforgery flow, OpenIddict wiring, forwarded headers, and hosted environment behavior.
---

# Langoose Auth Hosting

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill when the task touches auth flow, cookie behavior, antiforgery, OpenIddict, forwarded headers, Data Protection, or hosted environment assumptions.

## Main Rule

- Treat auth and hosting behavior as one operational boundary. Do not change cookies, CSRF, proxy trust, or token foundations in isolation.

## Workflow

- Inspect [Program.cs](../../../apps/api/src/Langoose.Api/Program.cs) first.
- Inspect the auth controllers and integration tests before changing behavior.
- Keep frontend bootstrap and API expectations aligned when auth status codes, antiforgery flow, or cookie assumptions change.
- When hosted behavior changes, update the relevant staging or deployment docs in `docs/` in the same change.

## Load Additional Detail Only When Needed

- For canonical repo guidance, read [auth-hosting.md](../../../docs/agent/auth-hosting.md).
