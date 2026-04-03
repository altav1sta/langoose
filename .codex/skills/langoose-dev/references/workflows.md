# Langoose Workflows

## Main Commands

- Backend build:
  - `dotnet build apps/api/Langoose.sln --configfile D:\Projects\langoose\apps\api\NuGet.Config`
- Backend tests:
  - `dotnet test apps/api/tests/Langoose.Api.Tests/Langoose.Api.Tests.csproj /p:RestoreConfigFile=D:\Projects\langoose\apps\api\NuGet.Config`
- Run API:
  - `dotnet run --project apps/api/src/Langoose.Api/Langoose.Api.csproj --configfile D:\Projects\langoose\apps\api\NuGet.Config`
- Frontend build:
  - `npm run build`
- Frontend dev:
  - `npm run dev`

Run frontend commands from `D:\Projects\langoose\apps\web`.

## API Reality Check

- Startup and registration live in `apps/api/src/Langoose.Api/Program.cs`.
- Shared persisted models and store abstractions live in `apps/api/src/Langoose.Domain`.
- Persistence goes through PostgreSQL and EF Core in `apps/api/src/Langoose.Data/AppDbContext.cs` and `apps/api/src/Langoose.Data/PostgresDataStore.cs`.
- Database initialization and base-content seeding live in `apps/api/src/Langoose.Data/Seeding/*` and `apps/api/src/Langoose.Data/Seeding/Json/base-store.json`.
- Dictionary rules live in `apps/api/src/Langoose.Api/Services/DictionaryService.cs`.
- Study scheduling and answer evaluation live in `apps/api/src/Langoose.Api/Services/StudyService.cs`.
- Controller contracts live under `apps/api/src/Langoose.Api/Controllers/`.

## Frontend Reality Check

- The app is currently centered in a single main component at `apps/web/src/App.tsx`.
- API contract helpers live in `apps/web/src/api.ts`.
- Styling is centralized in `apps/web/src/styles.css`.
- Avoid adding complexity unless the task clearly benefits from splitting components.

## Behavior Checks Before Editing

- Dictionary/import work:
  - Confirm visible items still collapse duplicate English terms across base and custom sources.
  - Confirm duplicate custom entries merge instead of multiplying.
  - Confirm base vocabulary overlap is skipped rather than duplicated.
  - Confirm invalid CSV headers and malformed rows do not partially import data.
- Study work:
  - Confirm exact matches still pass.
  - Confirm accepted variants, missing articles, inflection mismatches, and minor typos remain intentionally tolerant unless explicitly changed.
  - Confirm scheduling still updates due times deterministically.
- Clear-data work:
  - Confirm custom items, review state, imports, and flags are removed for the user.
  - Confirm active session tokens remain intact.

## Practical Cautions

- Avoid relying on files under `bin`, `obj`, `.vs`, `.dotnet`, and `node_modules` as if they were source files.
- If a change touches API behavior, inspect the discoverable xUnit tests first because they encode several product decisions more clearly than comments do.
- If frontend and backend contracts move together, update `apps/web/src/api.ts` in the same change.
