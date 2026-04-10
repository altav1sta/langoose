# Langoose

[Live staging](https://project-1lkky.vercel.app)

Langoose is a web app for Russian speakers learning English.

The product combines:

- a shared base vocabulary
- a private custom dictionary for each user
- CSV import and export for custom entries
- a study loop with server-side grading
- tolerant answer checking for close-but-acceptable answers

## Tech Stack

- Frontend: React 19, TypeScript, Vite
- Backend: ASP.NET Core Web API on .NET 10
- Data: PostgreSQL with EF Core
- Auth: ASP.NET Core Identity with cookie auth and antiforgery protection
- Tests: xUnit for backend, Vitest for frontend, Playwright-based e2e in Docker

## Repo Layout

- [`apps/api`](apps/api): backend
- [`apps/web`](apps/web): React frontend
- [`docs`](docs): supporting product and implementation docs
- [`CONTRIBUTING.md`](CONTRIBUTING.md): contribution workflow

## Quick Start

The easiest way to run the whole app locally is Docker Compose.

### Prerequisites

- Docker Desktop

### Run Everything

From the repo root:

```powershell
docker compose up --build
```

Then open:

- Web: `http://localhost:5173`
- API: `http://localhost:5000`
- API health: `http://localhost:5000/health`

To stop the stack:

```powershell
docker compose down
```

The compose stack includes:

- `web`
- `api`
- `postgres`

PostgreSQL data is stored in the Docker volume `langoose_postgres_data`.

## Local Development Without Docker

Use this path if you want to run the frontend and backend directly during development.

### Backend

The normal backend workflow is Visual Studio with [`apps/api/Langoose.sln`](apps/api/Langoose.sln).

Command-line alternative:

```powershell
dotnet build apps/api/Langoose.sln /p:RestoreConfigFile=apps/api/NuGet.Config
dotnet run --project apps/api/src/Langoose.Api/Langoose.Api.csproj
```

The API runs on `http://localhost:5000` by default.

### Frontend

From [`apps/web`](apps/web):

```powershell
npm install
npm run dev
```

The frontend dev server usually runs on `http://localhost:5173`.

If you need to override the API URL, copy `apps/web/.env.example` to `.env` and adjust `VITE_API_BASE_URL`.

## Docs

See [`docs/`](docs) for product decisions, architecture, deployment, and test strategy.

Agent instructions are in [`CLAUDE.md`](CLAUDE.md) and [`AGENTS.md`](AGENTS.md).
