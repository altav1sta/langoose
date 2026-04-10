# Langoose EF Core Structure Guidance

## Preferred Layout

- `apps/api/src/Langoose.Domain` for shared persisted models and domain-facing abstractions.
- `apps/api/src/Langoose.Data` for `AppDbContext`, configurations, migrations, and seeding.
- `apps/api/src/Langoose.Auth.Data` for auth `DbContext`, auth configuration, and auth migrations.
- `apps/api/tests/Langoose.Api.UnitTests`
- `apps/api/tests/Langoose.Api.IntegrationTests`

## What Belongs Where

- Keep EF-specific concerns out of controllers.
- Keep service-layer business rules in `apps/api/src/Langoose.Api/Services`.
- Keep shared persisted models in `apps/api/src/Langoose.Domain` when both API behavior and EF Core depend on them.
- Prefer one `IEntityTypeConfiguration<T>` file per entity once mappings stop being tiny.
- Prefer `ApplyConfigurationsFromAssembly` over a large inline mapping file.
- Treat repositories, extra abstractions, and separate migrations projects as opt-in complexity, not default architecture.

## Review Checklist

- Did the change reduce persistence coupling in `apps/api`?
- Are project references and namespaces straightforward?
- Are migrations still discoverable and runnable?
- Is `Program.cs` still simple?
- Did tests keep exercising business rules rather than EF internals?
