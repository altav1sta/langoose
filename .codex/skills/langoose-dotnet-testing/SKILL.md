---
name: langoose-dotnet-testing
description: Organize, review, or extend Langoose .NET tests using current ASP.NET Core and .NET guidance. Use when moving test projects, choosing between unit/integration/functional tests, setting up WebApplicationFactory or TestServer, or defining the preferred folder and project layout for the API test suite.
---

# Langoose Dotnet Testing

Read [D:\Projects\langoose\AGENTS.md](D:\Projects\langoose\AGENTS.md) first.

Use this skill when the task is mainly about .NET test structure, test boundaries, or ASP.NET Core test patterns.

## Preferred Layout For This Repo

- Keep production app code in `apps/`.
- Keep .NET test projects in a parallel root `tests/` folder.
- Prefer `tests/Langoose.Api.Tests` over `apps/api/tests`.
- If the suite grows, split by test type when that improves clarity or selective execution:
  - `tests/Langoose.Api.UnitTests`
  - `tests/Langoose.Api.IntegrationTests`
  - `tests/Langoose.Api.FunctionalTests`

## Organize By Test Type First

- Keep unit tests isolated from file system and other infrastructure.
- Keep integration tests for persistence and cross-component behavior.
- Keep functional tests for end-to-end HTTP behavior against the app host.
- Do not put infrastructure-heavy tests in the unit-test project.

## Organize Inside A Test Project

- Group tests by the application area they exercise, such as `Services`, `Controllers`, or a specific feature slice.
- Use folders and namespaces when many tests target the same class or feature.
- Prefer clear test names that describe method, scenario, and expected behavior.

## Langoose-Specific Recommendation

- The current executable harness in `apps/api/tests` behaves more like an integration/behavior suite than pure unit tests because it exercises the file-backed store and service interactions together.
- If it is moved, treat it as the first root-level API test project under `tests/`.
- If future pure unit tests are added for isolated logic like text normalization or answer evaluation helpers, keep those in a separate unit-test project.

## Load Additional Detail Only When Needed

- For the rationale and concrete repo conventions, read [D:\Projects\langoose\.codex\skills\langoose-dotnet-testing\references\layout-and-test-types.md](D:\Projects\langoose\.codex\skills\langoose-dotnet-testing\references\layout-and-test-types.md).
