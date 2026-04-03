---
name: langoose-efcore-structure
description: Design or refactor Langoose EF Core and PostgreSQL structure. Use when deciding project boundaries, moving persistence into a separate data project, organizing DbContext and migrations, splitting entity configuration classes, or defining EF Core conventions for apps/api and related data projects.
---

# Langoose EF Core Structure

Read [D:\Projects\langoose\AGENTS.md](D:\Projects\langoose\AGENTS.md) first.

Use this skill when the task is about EF Core structure rather than day-to-day feature work.

## Main Direction

- Prefer a separate data project when the PostgreSQL and EF Core layer is expected to grow.
- If persisted models need a shared home outside ASP.NET Core and EF Core, use `API + Domain + Data`.
- Keep `apps/api` responsible for startup, controllers, and service-layer behavior.
- Keep `apps/api/src/Langoose.Data` responsible for `DbContext`, EF configuration, migrations, persistence adapters, and database seeding components.
- Preserve existing business rules and public API behavior while moving persistence structure around.

## Target Shape

- `apps/api`
- `apps/api/src/Langoose.Domain`
- `apps/api/src/Langoose.Data`
- `apps/api/tests/Langoose.Api.Tests`

Inside the data project, prefer:

- `AppDbContext.cs`
- `Configurations/*`
- `Migrations/*`
- `PostgresDataStore.cs` or similarly named persistence adapters
- `Seeding/*` for database bootstrap components and seed data loaders

## Structure Rules

- Keep EF-specific concerns out of controllers.
- Keep service-layer business rules in `apps/api/src/Langoose.Api/Services`, even if persistence types move out.
- Keep shared persisted models and store abstractions in `apps/api/src/Langoose.Domain` when both API behavior and EF Core depend on them.
- Prefer one `IEntityTypeConfiguration<T>` file per entity once mappings are no longer tiny.
- Prefer `ApplyConfigurationsFromAssembly` in `DbContext` over a large inline mapping file.
- Keep the startup project explicit for EF tooling and runtime configuration.
- Do not split into a separate migrations project by default; use `API + Domain + Data` before adding a fourth boundary unless the task explicitly needs it.
- After moving or creating persistence files, normalize line endings immediately and verify that the new files do not contain mixed newlines.

## Workflow

- Inspect the current persistence files before moving them.
- Decide whether the change is only folder and namespace cleanup or a real project split.
- If introducing a data project, move EF runtime files together: `DbContext`, design-time factory, configurations, migrations, and persistence adapter types.
- If the persistence types are shared beyond EF, move them into `apps/api/src/Langoose.Domain` instead of keeping them under the API project.
- Start the refactor branch from the latest `main` branch so structural moves do not begin from an outdated base.
- Update startup wiring, project references, namespaces, design-time tooling, and CI or workflow paths together.
- Update repo guidance and skills if the persistence layout changes.

## Validation

- Run the current backend tests:
  - `dotnet test apps/api/tests/Langoose.Api.Tests/Langoose.Api.Tests.csproj /p:RestoreConfigFile=D:\Projects\langoose\apps\api\NuGet.Config`
- If project boundaries changed, also build the affected projects or solution explicitly.
- If startup or migrations wiring changed, verify the live container or local startup path before declaring the structure work complete.
- Before finalizing, run an explicit line-ending check on the new Domain and Data files so Visual Studio does not surface inconsistent newline prompts.

## Load Additional Detail Only When Needed

- For repo-wide working agreements and finish flow, use [D:\Projects\langoose\.codex\skills\langoose-dev\SKILL.md](D:\Projects\langoose\.codex\skills\langoose-dev\SKILL.md).
- For ASP.NET Core and EF-adjacent framework guidance, use [C:\Users\altav1sta\.codex\skills\aspnet-core\SKILL.md](C:\Users\altav1sta\.codex\skills\aspnet-core\SKILL.md).
- For the recommended Langoose layout and tradeoffs, read [D:\Projects\langoose\.codex\skills\langoose-efcore-structure\references\structure-guidance.md](D:\Projects\langoose\.codex\skills\langoose-efcore-structure\references\structure-guidance.md).
