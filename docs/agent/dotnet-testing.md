# .NET Testing

## Test Projects

- `apps/api/tests/Langoose.Core.UnitTests` — pure or infrastructure-light logic.
- `apps/api/tests/Langoose.Api.IntegrationTests` — host, persistence, auth, and cross-component behavior.
- `Langoose.Api.FunctionalTests` (optional) — add only if unit and integration tests are no longer enough.

## Boundary Rules

- Keep unit tests focused on infrastructure-light logic.
- Keep integration tests for persistence, host, auth, and cross-component behavior.
- If a test mainly proves isolated logic, a lightweight incidental dependency on
  framework objects does not automatically force it upward into integration.
- Keep `Api` integration tests for request-pipeline, auth, antiforgery, routing,
  and serialization behavior.
- Keep `Services` integration tests for EF-backed backend behavior where HTTP is
  not the primary risk.
- Do not add persistence-only tests unless they prove something the broader
  integration tests do not already prove.

## Organization

- Group tests by feature or application area.
- Use clear test names that describe method, scenario, and expected behavior.
- Keep application references one-way: test projects reference app projects.

## Review Checklist

- Does the test prove the right risk boundary (unit vs integration)?
- Is this infrastructure-heavy test in the right project?
- Does this add signal beyond broader integration coverage that already exists?
