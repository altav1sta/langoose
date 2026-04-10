---
name: langoose-dotnet-testing
description: Organize, review, or extend Langoose .NET tests using current ASP.NET Core and .NET guidance. Use when moving test projects, choosing between unit/integration/functional tests, setting up WebApplicationFactory or TestServer, or defining the preferred folder and project layout for the API test suite.
---

# Langoose Dotnet Testing

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill when the task is mainly about .NET test structure, test boundaries, or ASP.NET Core test patterns.

## Organize By Test Type First

- Keep unit tests isolated from file system and other infrastructure.
- Keep integration tests for persistence and cross-component behavior.
- Keep functional tests for end-to-end HTTP behavior against the app host.
- Do not put infrastructure-heavy tests in the unit-test project.
- If a test only uses lightweight framework objects incidentally while proving isolated logic, keep it with the shallowest layer that still matches the real risk.

## Organize Inside A Test Project

- Group tests by the application area they exercise, such as `Services`, `Controllers`, or a specific feature slice.
- Use folders and namespaces when many tests target the same class or feature.
- Prefer clear test names that describe method, scenario, and expected behavior.

## Load Additional Detail Only When Needed

- For the rationale and concrete repo conventions, read [dotnet-testing.md](../../../docs/agent/dotnet-testing.md).
