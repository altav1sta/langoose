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
- Run Worker:
  - `dotnet run --project apps/api/src/Langoose.Worker/Langoose.Worker.csproj`
  - polls `background_jobs` for `Pending` rows; needs Postgres up so the connection strings in `appsettings.json` resolve
- Frontend build (from `apps/web`):
  - `npm run build`
- Frontend tests (from `apps/web`):
  - `npm run test`
- Frontend dev (from `apps/web`):
  - `npm run dev`
- Whole app:
  - `docker compose up -d postgres`
  - `docker compose up -d app-api --build`
  - `docker compose up -d app-worker --build`
  - `docker compose up -d app-web --build`
- E2E tests (requires the whole app running):
  - `docker compose --profile e2e up --build e2e`

## Migrations

See [efcore-structure.md](efcore-structure.md) for migration commands, EF
conventions, entity details, and seeding.

## Bulk dictionary pipeline

The bulk-seed pipeline (corpus → import → AI validation → review →
promotion, see [dictionary-schema-design.md](dictionary-schema-design.md))
is driven by `Langoose.DbTool` job-submission commands and executed
asynchronously by `Langoose.Worker`. One row in `background_jobs` =
one batch run. The CLI inserts the first `Pending` row with
`StartCursor=null`; `CorpusImportJob` claims it, dispatches to
`ICorpusImportService.RunBatchAsync` for one batch, and on Completed
auto-creates a continuation `Pending` row with the next cursor. The
chain terminates when a run returns `Cursor=null` (no more data).

- Submit a bulk import:
  - `dotnet run --project apps/api/src/Langoose.DbTool -- submit-corpus-import --lang en --source <id>`
- List jobs:
  - `dotnet run --project apps/api/src/Langoose.DbTool -- list-jobs [--type CorpusImport] [--status Running] [--limit N]`
- Inspect a job:
  - `dotnet run --project apps/api/src/Langoose.DbTool -- show-job <id>`
- Cancel a Pending or Running job:
  - `dotnet run --project apps/api/src/Langoose.DbTool -- cancel-job <id>`
- Re-run a Failed or Cancelled job from its saved cursor:
  - `dotnet run --project apps/api/src/Langoose.DbTool -- resubmit-job <id>`
