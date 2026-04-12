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
- Frontend build (from `apps/web`):
  - `npm run build`
- Frontend tests (from `apps/web`):
  - `npm run test`
- Frontend dev (from `apps/web`):
  - `npm run dev`
- Whole app:
  - `docker compose up -d postgres`
  - `docker compose up -d api --build`
  - `docker compose up -d web --build`
- E2E tests (requires the whole app running):
  - `docker compose --profile e2e up --build e2e`

## Migrations

See [efcore-structure.md](efcore-structure.md) for migration commands, EF
conventions, entity details, and seeding.
