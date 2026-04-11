---
name: langoose-efcore-structure
description: Design or refactor Langoose EF Core and PostgreSQL structure. Use when deciding project boundaries, moving persistence into a separate data project, organizing DbContext and migrations, splitting entity configuration classes, or defining EF Core conventions for apps/api and related data projects.
---

# Langoose EF Core Structure

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill when the task is about EF Core structure rather than day-to-day feature work.

## Main Direction

- Prefer a separate data project when the PostgreSQL and EF Core layer is expected to grow.
- If persisted models need a shared home outside ASP.NET Core and EF Core, use `API + Domain + Data`.
- Preserve existing business rules and public API behavior while moving persistence structure around.

## Structure Rules

- Keep EF-specific concerns out of controllers.
- Keep service-layer business rules in `apps/api/src/Langoose.Api/Services`, even if persistence types move out.
- Keep shared persisted models in `apps/api/src/Langoose.Domain` when both API behavior and EF Core depend on them, but avoid carrying broad persistence-container abstractions there once services can use focused EF access directly.
- Prefer one `IEntityTypeConfiguration<T>` file per entity once mappings are no longer tiny.
- Prefer `ApplyConfigurationsFromAssembly` in `DbContext` over a large inline mapping file.
- Keep the startup project explicit for EF tooling and runtime configuration.
- Do not split into a separate migrations project by default; use `API + Domain + Data` before adding a fourth boundary unless the task explicitly needs it.
- After moving or creating persistence files, normalize line endings immediately and verify that the new files do not contain mixed newlines.

## Workflow

- Inspect the current persistence files before moving them.
- Decide whether the change is only folder and namespace cleanup or a real project split.
- If introducing or splitting data projects, move EF runtime files together: `DbContext`, design-time factory, configurations, migrations, and seeding types.
- If the persistence types are shared beyond EF, move them into `apps/api/src/Langoose.Domain` instead of keeping them under the API project.
- Update startup wiring, project references, namespaces, design-time tooling, and CI or workflow paths together.
- Update repo guidance and skills if the persistence layout changes.

## Validation

- Run the current backend tests:
  - `dotnet test apps/api/tests/Langoose.Core.UnitTests/Langoose.Core.UnitTests.csproj /p:RestoreConfigFile=apps/api/NuGet.Config`
  - `dotnet test apps/api/tests/Langoose.Api.IntegrationTests/Langoose.Api.IntegrationTests.csproj /p:RestoreConfigFile=apps/api/NuGet.Config`
- If project boundaries changed, also build the affected projects or solution explicitly.
- If startup or migrations wiring changed, verify the live container or local startup path before declaring the structure work complete.
- Before finalizing, run an explicit line-ending check on the new Domain and Data files so Visual Studio does not surface inconsistent newline prompts.

## Load Additional Detail Only When Needed

- For repo-wide working agreements and finish flow, use [langoose-dev](../langoose-dev/SKILL.md).
- For ASP.NET Core and EF-adjacent framework guidance, use the shared `aspnet-core` skill when it is available in the current Codex environment.
- For the recommended Langoose layout and tradeoffs, read [efcore-structure.md](../../../docs/agent/efcore-structure.md).
