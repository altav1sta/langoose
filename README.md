# Langoose

Langoose is a web-first MVP for Russian speakers learning English. It combines sentence-based cloze practice, deterministic spaced repetition, a seeded base vocabulary, and a private custom dictionary.

## Stack

- Frontend: React + TypeScript
- Frontend tooling: Vite
- Styling: plain CSS
- Backend: ASP.NET Core Web API on .NET 10 / C#
- API style: controller-based JSON API
- Persistence: PostgreSQL-backed API storage
- Auth: deployable MVP target is first-party email/password on an OAuth/OIDC-capable backend; current code still uses a placeholder token flow
- Tests: xUnit-based .NET test project under `tests/`

## Repo layout

- [`apps/api`](apps/api): backend API, .NET solution, and config
- [`tests/Langoose.Api.Tests`](tests/Langoose.Api.Tests): discoverable xUnit backend behavior tests
- [`apps/web`](apps/web): frontend SPA
- [`docs`](docs): roadmap and project documentation
- `.local`: local-only cache used in restricted environments
- root: shared repo files only

## Project planning

- Roadmap overview: [`docs/roadmap.md`](docs/roadmap.md)
- Auth decision note: [`docs/auth-mvp-decision.md`](docs/auth-mvp-decision.md)
- Auth implementation blueprint: [`docs/auth-m1-implementation-blueprint.md`](docs/auth-m1-implementation-blueprint.md)
- Live execution tracking: GitHub `Langoose MVP` project
- Contribution workflow: [`CONTRIBUTING.md`](CONTRIBUTING.md)

## What the MVP currently does

- Signs in with a simple email/name flow
- Seeds a base English vocabulary set for Russian-speaking learners
- Lets users add private custom words and phrases
- Imports and exports custom dictionary items as CSV
- Prevents duplicate custom entries and skips terms already present in the base dictionary
- Runs a sentence-based study loop with server-side answer checking
- Supports tolerant grading for exact matches, known variants, missing articles, inflection mismatches, and minor typos
- Lets users report bad cards and clear all custom data

## Current implementation notes

- The current MVP uses PostgreSQL-backed persistence and the local Docker stack runs the API, web app, and PostgreSQL together.
- The current auth flow in code is still a placeholder email/name flow and is not production auth.
- The planned auth direction is first-party email/password on top of a web-first, future-native-compatible backend.
- CSV import is file-only.
- CSV files must start with these columns in this order:
  - `English term`
  - `Russian translation(s)`
  - `Type`
- Optional extra columns:
  - `Notes`
  - `Tags`
- Invalid CSV structure is rejected before any rows are imported.

## Running the backend

The normal local workflow is Visual Studio:

1. Open [`apps/api/Langoose.sln`](apps/api/Langoose.sln)
2. Run the API from Visual Studio

By default the API is configured to run on:

- `http://localhost:5000`

Useful endpoints:

- `GET /`
- `GET /health`

If you need the command-line flow:

```powershell
dotnet build apps/api/Langoose.sln --configfile D:\Projects\langoose\apps\api\Langoose.Api\NuGet.Config
dotnet run --project apps/api/Langoose.Api/Langoose.Api.csproj --configfile D:\Projects\langoose\apps\api\Langoose.Api\NuGet.Config
```

Run backend checks:

```powershell
dotnet test tests/Langoose.Api.Tests/Langoose.Api.Tests.csproj /p:RestoreConfigFile=D:\Projects\langoose\apps\api\Langoose.Api\NuGet.Config
```

## CI checks

GitHub Actions runs these pull-request checks:

- `CI / Backend Build`
- `CI / Backend Tests`
- `CI / Frontend Build`

The workflow does not require repository secrets. These check names are intended to stay stable so branch protection
rules can require them before merge.

## Running the backend with Docker

The API can also run in containers with PostgreSQL as the persistent runtime data store.

Build and start it from the repo root:

```powershell
docker compose up --build api
```

The container publishes the API on:

- `http://localhost:5000`

Useful notes:

- The image is built from [`apps/api/Langoose.Api/Dockerfile`](apps/api/Langoose.Api/Dockerfile)
- Persistent API data is stored in the named Docker volume `langoose_api_data`
- Inside the containerized stack, application data is stored in PostgreSQL

To stop the container:

```powershell
docker compose down
```

## Running the full Docker stack

The repo also includes a local multi-service Docker setup for the web app, API, and PostgreSQL.

Start everything from the repo root:

```powershell
docker compose up --build
```

Use `docker compose up` when the images are already current. Use `docker compose up --build` after changing
Dockerfiles or files that are copied into the images.

The stack publishes:

- web: `http://localhost:5173`
- api: `http://localhost:5000`
- postgres: `localhost:5432`

Useful notes:

- The web container is built from [`apps/web/Dockerfile`](apps/web/Dockerfile)
- The web container injects `LANGOOSE_API_BASE_URL` at startup so the same image can target different API hosts
- PostgreSQL is the runtime data store for the current API
- PostgreSQL data is persisted in the named volume `langoose_postgres_data`

To stop the full stack:

```powershell
docker compose down
```

## Running the frontend

The frontend lives in `apps/web`.

1. Copy `.env.example` to `.env` if you want to override the API URL
2. Install dependencies
3. Start the Vite dev server

```powershell
cd apps/web
npm install
npm run dev
```

Open the local URL shown by Vite, usually:

- `http://localhost:5173`

## Typical local test flow

1. Start the backend
2. Start the frontend
3. Sign in with the current placeholder email/name flow
4. Add a custom word or phrase
5. Import a valid CSV file
6. Study a few cards
7. Export CSV
8. Clear custom data if needed

## Ignore strategy

- root `.gitignore`: shared repo/workstation noise plus repo-wide .NET build/test outputs
- `apps/api/.gitignore`: .NET outputs and backend-local runtime files
- `apps/web/.gitignore`: Node and frontend build artifacts
