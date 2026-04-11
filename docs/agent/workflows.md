# Langoose Workflows

## Main Commands

- Backend build:
  - `dotnet build apps/api/Langoose.sln /p:RestoreConfigFile=apps/api/NuGet.Config`
- Backend unit tests:
  - `dotnet test apps/api/tests/Langoose.Core.UnitTests/Langoose.Core.UnitTests.csproj /p:RestoreConfigFile=apps/api/NuGet.Config`
- Backend integration tests:
  - `dotnet test apps/api/tests/Langoose.Api.IntegrationTests/Langoose.Api.IntegrationTests.csproj /p:RestoreConfigFile=apps/api/NuGet.Config`
- Run API:
  - `dotnet run --project apps/api/src/Langoose.Api/Langoose.Api.csproj`
- Frontend build:
  - `npm run build`
- Frontend tests:
  - `npm run test`
- Frontend dev:
  - `npm run dev`
- Whole app:
  - `docker compose up -d postgres`
  - `docker compose up -d api --build`
  - `docker compose up -d web --build`
- E2E tests (requires the whole app running):
  - `docker compose --profile e2e up --build e2e`

Run frontend commands from `apps/web`.

## Migrations

Both data projects use `Microsoft.EntityFrameworkCore.Design` and rely on
`Langoose.Api` as the startup project for design-time context resolution.

### Creating a migration

App database (`AppDbContext` in `Langoose.Data`):

```
dotnet ef migrations add <MigrationName> --project apps/api/src/Langoose.Data --startup-project apps/api/src/Langoose.Api
```

Auth database (`AuthDbContext` in `Langoose.Auth.Data`):

```
dotnet ef migrations add <MigrationName> --project apps/api/src/Langoose.Auth.Data --startup-project apps/api/src/Langoose.Api
```

### Applying migrations

- **Local / Docker**: the API applies migrations automatically on startup.
- **Staging / Production**: migrations are applied explicitly before deploy via
  `Langoose.DbTool` (`apply-app-migrations`, `apply-auth-migrations`).
  See `.github/workflows/app-db-migrations.yml` and `auth-db-migrations.yml`.

### Seeding

Base content seeding uses `Langoose.DbTool seed-app`. It is empty-database-only —
run it only when the app database is brand new or has been recreated.
See `.github/workflows/app-seed.yml` for the hosted workflow.

## Repo Reality Check

- Startup and registration live in `apps/api/src/Langoose.Api/Program.cs`.
- Domain models, enums, constants, and service interfaces live in `apps/api/src/Langoose.Domain`.
- Service implementations and utilities live in `apps/api/src/Langoose.Core`.
- App data persistence lives in `apps/api/src/Langoose.Data`.
- Auth persistence lives in `apps/api/src/Langoose.Auth.Data`.
- Background processing host lives in `apps/api/src/Langoose.Worker`.
- Database tooling lives in `apps/api/src/Langoose.DbTool`.
- API contract helpers live in `apps/web/src/api.ts`.
- The current frontend is centered in `apps/web/src/App.tsx`.

## CI Alignment

GitHub Actions workflows under `.github/workflows/` reference project paths,
Dockerfile paths, and test project names directly. When a change renames or
moves a project, Dockerfile, or test assembly, update the affected workflow
files in the same change to keep CI green.

## Practical Cautions

- Avoid relying on `bin`, `obj`, `.vs`, `.dotnet`, and `node_modules` as if they were source files.
- If a change touches API behavior, inspect tests under `apps/api/tests` because they encode product decisions clearly.
- If frontend and backend contracts move together, update `apps/web/src/api.ts` in the same change.
- For auth, cookie, antiforgery, OpenIddict, forwarded-header, or hosted environment changes, use `langoose-auth-hosting` and keep the related `docs/` guidance aligned.
