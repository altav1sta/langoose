---
name: langoose-architecture
description: Design or refactor Langoose project boundaries and dependency direction. Use when choosing between API-only, API plus Data, or API plus Domain plus Data structures, or when applying lightweight onion or clean architecture to apps/api and related projects.
---

# Langoose Architecture

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill when the task is about architecture and project boundaries, not just file moves.

## Main Direction

- Keep the dependency graph simple and explicit.
- Introduce new projects only when they solve a real ownership or growth problem.
- Prefer lightweight onion boundaries over ceremony-heavy clean architecture.

## Workflow

- Identify the ownership problem first: growth, dependency cycle, tooling confusion, or mixed responsibilities.
- Choose the smallest project split that fixes that problem.
- Update solution files, project references, Docker build context, and test references together.
- Update CI/workflow paths together with the structure change whenever build, test, Docker, or restore commands depend on moved files.
- Update repo guidance and relevant skills when the architecture decision changes the expected project layout.

## Validation

- Build the affected solution or projects explicitly after changing project boundaries.
- Run the current backend tests:
  - `dotnet test apps/api/tests/Langoose.Api.UnitTests/Langoose.Api.UnitTests.csproj /p:RestoreConfigFile=apps/api/NuGet.Config`
  - `dotnet test apps/api/tests/Langoose.Api.IntegrationTests/Langoose.Api.IntegrationTests.csproj /p:RestoreConfigFile=apps/api/NuGet.Config`
- If Docker or startup wiring changed, verify the live startup path before declaring the architecture work complete.
- If the refactor created new projects or moved many files, check for mixed line endings before finalizing.

## Load Additional Detail Only When Needed

- For EF Core-specific `API + Data` structure guidance, use [langoose-efcore-structure](../langoose-efcore-structure/SKILL.md).
- For repo-wide implementation and finish discipline, use [langoose-dev](../langoose-dev/SKILL.md).
- For the lightweight onion-style recommendations in this repo, read [architecture-guidance.md](../../../docs/agent/architecture-guidance.md).
