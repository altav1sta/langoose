# Langoose Auth And Hosting Guidance

## Scope

Use this guidance when a change touches any of the following:

- auth cookies
- antiforgery flow
- `GET /auth/me`, sign-in, sign-up, or sign-out behavior
- OpenIddict wiring
- forwarded headers and trusted proxy handling
- Data Protection keys
- hosted staging or production assumptions

## Main Boundary

- Treat browser auth, antiforgery, proxy trust, and hosted runtime behavior as one operational system.
- Do not change cookie, CSRF, proxy, or token-foundation behavior in isolation.
- Keep the frontend bootstrap flow aligned with backend auth status codes and antiforgery expectations.

## Main Files

- `apps/api/src/Langoose.Api/Program.cs`
- `apps/api/src/Langoose.Api/Controllers/AuthController.cs`
- `apps/web/src/api.ts`
- `apps/api/tests/Langoose.Api.IntegrationTests`
- staging and deployment docs under `docs/`

## Review Checklist

- Did cookie and antiforgery behavior stay compatible with the browser origin and deployment model?
- Did forwarded-header trust stay explicit for hosted environments?
- Are auth and app connection settings validated clearly at startup?
- If OpenIddict stays enabled, are signing and encryption credentials appropriate for the target environment?
- If the app runs in Docker or hosted environments, are ASP.NET Core Data Protection keys persisted appropriately?
- Did auth bootstrap and authenticated write flows stay covered by integration or end-to-end validation?

## Current Repo Direction

- Cookie auth plus antiforgery is the current browser session model.
- `Langoose.Auth.Data` is the auth persistence boundary.
- Forwarded headers are enabled only through explicit config.
- Hosted auth behavior must remain aligned with the staging and deployment docs in `docs/`.

## Best-Practice Notes

- Prefer strongly validated options for auth, CORS, and forwarded-header settings instead of ad hoc configuration reads when this area is refactored.
- Persist ASP.NET Core Data Protection keys in shared or hosted environments so auth cookies remain decryptable across restarts and instances.
- Use non-ephemeral OpenIddict signing and encryption credentials outside throwaway local development.
- Keep readiness and deployment checks explicit when auth or persistence startup behavior changes.

## Sources

- Microsoft Learn: [Options pattern in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0)
- Microsoft Learn: [Health checks in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0)
- Microsoft Learn: [Data Protection configuration overview](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0)
- OpenIddict: [Encryption and signing credentials](https://documentation.openiddict.com/configuration/encryption-and-signing-credentials)
