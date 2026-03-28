# Langoose

Langoose is a web-first MVP for Russian speakers learning English. It combines sentence-based cloze practice, deterministic spaced repetition, a seeded base vocabulary, and a private custom dictionary.

## Stack

- Frontend: React + TypeScript
- Frontend tooling: Vite
- Styling: plain CSS
- Backend: ASP.NET Core Web API on .NET 10 / C#
- API style: controller-based JSON API
- Persistence: local file-backed JSON store
- Auth: lightweight token-based MVP auth
- Tests: xUnit-based .NET test project under `tests/`

## Repo layout

- `apps/api`: backend API, .NET solution, and config
- `tests/Langoose.Api.Tests`: discoverable xUnit backend behavior tests
- `apps/web`: frontend SPA
- `docs`: roadmap and project documentation
- `.local`: local-only cache used in restricted environments
- root: shared repo files only

## Project planning

- Roadmap overview: `docs/roadmap.md`
- Live execution tracking: GitHub `Langoose MVP` project
- Contribution workflow: `CONTRIBUTING.md`

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

- The original product plan mentioned PostgreSQL, but this MVP still uses a local JSON file store so it can run without extra infrastructure.
- The current auth flow is MVP-only and not production auth.
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

1. Open `apps/api/Langoose.Api.sln`
2. Run the API from Visual Studio

By default the API is configured to run on:

- `http://localhost:5000`

Useful endpoints:

- `GET /`
- `GET /health`

If you need the command-line flow:

```powershell
dotnet build apps/api/Langoose.Api.csproj --configfile D:\Projects\langoose\apps\api\NuGet.Config
dotnet run --project apps/api/Langoose.Api.csproj --configfile D:\Projects\langoose\apps\api\NuGet.Config
```

Run backend checks:

```powershell
dotnet test tests/Langoose.Api.Tests/Langoose.Api.Tests.csproj /p:RestoreConfigFile=D:\Projects\langoose\apps\api\NuGet.Config
```

## Running the backend with Docker

The API can also run in a Linux container with its JSON store mounted as persistent runtime data.

Build and start it from the repo root:

```powershell
docker compose up --build api
```

The container publishes the API on:

- `http://localhost:5000`

Useful notes:

- The image is built from `apps/api/Dockerfile`
- Persistent API data is stored in the named Docker volume `langoose_api_data`
- Inside the container, the JSON store lives at `/app/data/store.json`

To stop the container:

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
3. Sign in with any email and name
4. Add a custom word or phrase
5. Import a valid CSV file
6. Study a few cards
7. Export CSV
8. Clear custom data if needed

## Ignore strategy

- root `.gitignore`: shared repo/workstation noise plus repo-wide .NET build/test outputs
- `apps/api/.gitignore`: .NET outputs and backend-local runtime files
- `apps/web/.gitignore`: Node and frontend build artifacts
