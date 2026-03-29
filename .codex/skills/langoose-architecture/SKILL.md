---
name: langoose-architecture
description: Design or refactor Langoose project boundaries and dependency direction. Use when choosing between API-only, API plus Data, or API plus Domain plus Data structures, or when applying lightweight onion or clean architecture to apps/api and related projects.
---

# Langoose Architecture

Read [D:\Projects\langoose\AGENTS.md](D:\Projects\langoose\AGENTS.md) first.

Use this skill when the task is about architecture and project boundaries, not just file moves.

## Main Direction

- Prefer a lightweight onion-style structure over a ceremony-heavy clean-architecture template.
- Keep the dependency graph simple and explicit.
- Introduce new projects only when they solve a real ownership or growth problem.

## Preferred Shapes

- For modest EF growth: `API + Data`
- For larger or longer-lived core modeling work: `API + Domain + Data`

Preferred dependency direction:

- `Domain` has no dependency on `API` or `Data`
- `Data` depends on `Domain`
- `API` depends on `Domain` and `Data`

## What Each Project Owns

- `apps/api/Langoose.Api`
  - `Program.cs`
  - controllers
  - service-layer orchestration and application behavior
- `apps/api/Langoose.Domain`
  - core domain models
  - enums/value objects that represent business concepts
  - business-facing abstractions that should not depend on EF Core or ASP.NET Core
- `apps/api/Langoose.Data`
  - `DbContext`
  - EF configurations
  - migrations
  - persistence adapters and database wiring

## Rules

- Do not introduce all the usual "clean architecture" ceremony by default.
- Avoid adding CQRS, mediator layers, repository-per-entity patterns, or use-case classes unless the task explicitly calls for them.
- Keep business rules readable and close to the current service layer unless there is a clear reason to move them inward.
- Keep EF Core and ASP.NET Core concerns out of the domain project.
- Prefer stable namespaces and low-churn refactors when splitting projects.
- After creating or moving many files across projects, normalize line endings before finishing so the architectural cleanup does not create IDE noise.

## Workflow

- Identify the ownership problem first: growth, dependency cycle, tooling confusion, or mixed responsibilities.
- Choose the smallest project split that fixes that problem.
- If the current models must be shared between API behavior and EF persistence, decide whether they belong in `Domain` before creating a data project.
- Start the refactor branch from the latest `main` branch so structural moves do not begin from an outdated base.
- Update solution files, project references, Docker build context, and test references together.
- Update repo guidance and relevant skills when the architecture decision changes the expected project layout.
- Before opening the PR for a structural refactor, sync the branch with the current `main` branch and resolve merge
  conflicts locally so the project move does not leave GitHub with unresolved rename conflicts.

## Validation

- Build the affected solution or projects explicitly after changing project boundaries.
- Run the current backend tests:
  - `dotnet test tests/Langoose.Api.Tests/Langoose.Api.Tests.csproj /p:RestoreConfigFile=D:\Projects\langoose\apps\api\Langoose.Api\NuGet.Config`
- If Docker or startup wiring changed, verify the live startup path before declaring the architecture work complete.
- If the refactor created new projects or moved many files, check for mixed line endings before finalizing.
- If a validation step fails, is blocked by the environment, or does not complete, do not report it as passing from
  memory or inference. State the gap plainly, rerun it if possible, and only claim success after a real successful run.
- Before reporting the refactor as ready for review, verify that the PR is actually mergeable after syncing with
  `main`.

## Load Additional Detail Only When Needed

- For EF Core-specific `API + Data` structure guidance, use [D:\Projects\langoose\.codex\skills\langoose-efcore-structure\SKILL.md](D:\Projects\langoose\.codex\skills\langoose-efcore-structure\SKILL.md).
- For repo-wide implementation and finish discipline, use [D:\Projects\langoose\.codex\skills\langoose-dev\SKILL.md](D:\Projects\langoose\.codex\skills\langoose-dev\SKILL.md).
- For the lightweight onion-style recommendations in this repo, read [D:\Projects\langoose\.codex\skills\langoose-architecture\references\architecture-guidance.md](D:\Projects\langoose\.codex\skills\langoose-architecture\references\architecture-guidance.md).
