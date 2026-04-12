---
name: langoose-dotnet-testing
description: Organize, review, or extend Langoose .NET tests using current ASP.NET Core and .NET guidance. Use when moving test projects, choosing between unit/integration/functional tests, setting up WebApplicationFactory or TestServer, or defining the preferred folder and project layout for the API test suite.
---

# Langoose Dotnet Testing

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill when the task is mainly about .NET test structure, test boundaries, or ASP.NET Core test patterns.

## Use When

- You are choosing between unit, integration, and functional coverage.
- You are moving or reorganizing backend tests.
- You are setting up or reviewing ASP.NET Core host-based tests.

## Primary Doc

- [dotnet-testing.md](../../../docs/agent/dotnet-testing.md)

## Related Docs

- [quality-gates.md](../../../docs/agent/quality-gates.md)

## Critical Reminders

- Keep unit tests isolated from file system and other infrastructure.
- Keep integration tests for persistence and cross-component behavior.
- Keep functional tests for end-to-end HTTP behavior against the app host.
- Do not put infrastructure-heavy tests in the unit-test project.
- If a test only uses lightweight framework objects incidentally while proving isolated logic, keep it with the shallowest layer that still matches the real risk.
- Group tests by the application area they exercise, such as `Services`, `Controllers`, or a specific feature slice.
- Use folders and namespaces when many tests target the same class or feature.
- Prefer clear test names that describe method, scenario, and expected behavior.
