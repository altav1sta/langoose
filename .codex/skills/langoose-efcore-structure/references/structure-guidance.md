# Langoose EF Core Structure Guidance

## Preferred Layout

Use this when Langoose persistence is growing beyond a small in-project EF setup and the persisted models need a shared home:

- `apps/api`
  - controllers
  - services
  - startup and configuration
- `apps/api/src/Langoose.Domain`
  - shared persisted models
  - store abstractions such as `IDataStore`
  - enums and value-like types used by both the API and persistence
- `apps/api/src/Langoose.Data`
  - `AppDbContext.cs`
  - `Configurations/*`
  - `Migrations/*`
  - persistence adapter types such as `PostgresDataStore`
  - database seeders and seed data loaders
- `apps/api/tests/Langoose.Api.Tests`

## Why `API + Domain + Data`

- It keeps ASP.NET Core host concerns separate from EF Core persistence concerns.
- It gives shared persisted models a stable home outside both the API and EF projects.
- It keeps the first architectural split meaningful without adding a separate migrations project yet.
- It leaves room to add more application or deployment boundaries later if the repo truly needs them.

## What Belongs In The Data Project

- `DbContext`
- `IDesignTimeDbContextFactory`
- EF model configuration classes
- migrations and model snapshot
- EF-backed persistence adapters
- database seeders and seed data loaders

## What Belongs In The Domain Project

- shared persisted models
- enums and value-like types referenced by both API services and EF Core
- store abstractions that should not depend on ASP.NET Core or EF Core

## What Stays In The API Project

- `Program.cs`
- controllers
- service-layer business rules
- request and response models that are part of the API surface

## Review Checklist

- Did the change reduce persistence coupling in `apps/api`?
- Are project references and namespaces straightforward?
- Are migrations still discoverable and runnable?
- Is `Program.cs` still simple?
- Did tests keep exercising business rules rather than EF internals?
