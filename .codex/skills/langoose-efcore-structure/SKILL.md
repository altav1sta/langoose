---
name: langoose-efcore-structure
description: Design or refactor Langoose EF Core and PostgreSQL structure. Use when organizing DbContext and migrations, splitting entity configuration classes, defining EF Core conventions, or refining persistence layout inside the existing backend structure.
---

# Langoose EF Core Structure

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill when the task is about EF Core structure rather than day-to-day feature work.

## Use When

- The task changes `DbContext`, entity configuration, migrations, or seeding layout.
- The task changes EF conventions or tooling setup.
- The task changes persistence layout inside the existing backend structure.

## Primary Doc

- [efcore-structure.md](../../../docs/agent/efcore-structure.md)

## Related Docs

- [workflows.md](../../../docs/agent/workflows.md)
- [quality-gates.md](../../../docs/agent/quality-gates.md)

## Critical Reminders

- Prefer a separate data project when the PostgreSQL and EF Core layer is expected to grow.
- Preserve existing business rules and public API behavior while moving persistence structure around.
- Keep EF-specific concerns out of controllers.
- Keep service-layer business rules in `apps/api/src/Langoose.Core/Services`, even if persistence types move out.
- Keep shared persisted models in `apps/api/src/Langoose.Domain` when both API behavior and EF Core depend on them, but avoid carrying broad persistence-container abstractions there once services can use focused EF access directly.
- Prefer one `IEntityTypeConfiguration<T>` file per entity once mappings are no longer tiny.
- Prefer `ApplyConfigurationsFromAssembly` in `DbContext` over a large inline mapping file.
- Keep the startup project explicit for EF tooling and runtime configuration.
- After moving or creating persistence files, normalize line endings immediately and verify that the new files do not contain mixed newlines.

## Workflow

- Inspect the current persistence files before moving them.
- Decide whether the change is only folder and namespace cleanup or a targeted persistence-layout change.
- Move EF runtime files together when needed: `DbContext`, design-time factory, configurations, migrations, and seeding types.
- Update startup wiring, namespaces, design-time tooling, and CI or workflow paths together when persistence files move.
- Update repo guidance and skills if the persistence layout changes.

## Validation

- Run the current backend tests:
  - `dotnet test apps/api/tests/Langoose.Core.UnitTests/Langoose.Core.UnitTests.csproj /p:RestoreConfigFile=apps/api/NuGet.Config`
  - `dotnet test apps/api/tests/Langoose.Api.IntegrationTests/Langoose.Api.IntegrationTests.csproj /p:RestoreConfigFile=apps/api/NuGet.Config`
- If startup or migrations wiring changed, verify the live container or local startup path before declaring the structure work complete.
- Before finalizing, run an explicit line-ending check on the new Domain and Data files so Visual Studio does not surface inconsistent newline prompts.
