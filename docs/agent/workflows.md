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

## Bulk dictionary pipeline

The bulk-seed pipeline (corpus → import → AI validation → review →
promotion, see [dictionary-schema-design.md](dictionary-schema-design.md))
is driven by `Langoose.DbTool` job-submission commands and executed
asynchronously by `Langoose.Worker`'s generic `BackgroundJobService`. The
CLI inserts a `Pending` row into `background_jobs`; the worker picks it
up, dispatches to the matching handler (`BulkImportJobHandler` for
`BulkImport`), streams corpus rows in batches, and persists progress +
cursor in `ExecutionState` so a worker restart resumes mid-job.

- Submit a bulk import:
  - `dotnet run --project apps/api/src/Langoose.DbTool -- submit-bulk-import --lang en --wiktionary-source <id> --wordfreq-source <id> [--top-rank N] [--limit N]`
- List jobs:
  - `dotnet run --project apps/api/src/Langoose.DbTool -- list-jobs [--type BulkImport] [--status Running] [--limit N]`
- Inspect a job:
  - `dotnet run --project apps/api/src/Langoose.DbTool -- show-job <id>`
- Cancel a Pending or Running job:
  - `dotnet run --project apps/api/src/Langoose.DbTool -- cancel-job <id>`
- Re-run a Failed or Cancelled job from its saved cursor:
  - `dotnet run --project apps/api/src/Langoose.DbTool -- resubmit-job <id>`
