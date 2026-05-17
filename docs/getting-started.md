# Getting Started

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) and npm
- [Docker](https://www.docker.com/) and Docker Compose (for PostgreSQL)
- [Git](https://git-scm.com/)
- Bash (Git Bash or WSL on Windows) — required for `scripts/*.sh`

## Quick Start

### 1. Set up environment variables

```bash
cp .env.example .env
```

`compose.yml` reads `.env` for service environment variables. The
template carries safe local-dev defaults; tweak `.env` (gitignored) for
per-machine overrides like `POSTGRES_DATA_PATH=...`.

### 2. Start the database

```bash
docker compose up -d postgres
```

This starts PostgreSQL on port 5432 with three databases: `langoose_app`,
`langoose_auth`, and `langoose_corpus`. Credentials are in `.env`.

### 3. Run the API

```bash
dotnet run --project apps/api/src/Langoose.Api/Langoose.Api.csproj
```

On first run, the API auto-applies migrations and seeds base dictionary content.
The API listens on `http://localhost:5000` by default.

### 4. Run the frontend

```bash
cd apps/web
npm install
npm run dev
```

The frontend runs on `http://localhost:5173` and proxies API calls to port 5000.

### 5. Run the Worker

```bash
dotnet run --project apps/api/src/Langoose.Worker/Langoose.Worker.csproj
```

The Worker also hosts a minimal Kestrel listener on `http://localhost:5050`
(distinct from the API's `:5000`) to expose `/health` for orchestrator
liveness probes; you can ignore it locally.

The Worker polls `background_jobs` for `Pending` rows and dispatches them to
the matching handler — currently the bulk-import pipeline (see
[agent/workflows.md](agent/workflows.md) → "Bulk dictionary pipeline") and the
enrichment loop. Run it whenever you submit a job via `Langoose.DbTool` or
want to exercise enrichment.

The Worker connects to `langoose_app` and `langoose_corpus` using the
connection strings in its `appsettings.json`. The API auto-applies app-database
migrations on startup, so start the API at least once first (or run
`dotnet run --project apps/api/src/Langoose.DbTool -- apply-app-migrations`)
before starting the Worker against an empty database.

## Running with Docker Compose

To run everything containerized:

```bash
docker compose up -d postgres
docker compose up -d app-api --build
docker compose up -d app-worker --build
docker compose up -d app-web --build
```

## Build and Test

### Backend

```bash
# Build
dotnet build apps/api/Langoose.sln /p:RestoreConfigFile=apps/api/NuGet.Config

# Unit tests
dotnet test apps/api/tests/Langoose.Core.UnitTests/Langoose.Core.UnitTests.csproj \
  /p:RestoreConfigFile=apps/api/NuGet.Config

# Integration tests
dotnet test apps/api/tests/Langoose.Api.IntegrationTests/Langoose.Api.IntegrationTests.csproj \
  /p:RestoreConfigFile=apps/api/NuGet.Config
```

### Frontend

```bash
cd apps/web
npm run build    # production build
npm run test     # run tests
```

### E2E Tests

Requires the whole app running via Docker Compose:

```bash
docker compose --profile e2e up --build e2e
```

## Migrations

App database:

```bash
dotnet ef migrations add <MigrationName> \
  --project apps/api/src/Langoose.Data \
  --startup-project apps/api/src/Langoose.DbTool \
  --context AppDbContext
```

Auth database:

```bash
dotnet ef migrations add <MigrationName> \
  --project apps/api/src/Langoose.Auth.Data \
  --startup-project apps/api/src/Langoose.DbTool \
  --context AuthDbContext
```

Migrations are auto-applied on startup locally. For staging/production they
run from CI via [`app-db-migrations.yml`](../.github/workflows/app-db-migrations.yml)
and [`auth-db-migrations.yml`](../.github/workflows/auth-db-migrations.yml),
which invoke `Langoose.DbTool`'s `apply-app-migrations` / `apply-auth-migrations`
subcommands against the target environment's connection strings.

## Project Structure

See [architecture.md](architecture.md) for the full project map and onion layer
description.

## Configuration

The API reads configuration from `appsettings.json`:

| Section | Key settings |
|---------|-------------|
| ConnectionStrings | AppDatabase, AuthDatabase, CorpusDatabase |
| Cors | AllowedOrigins |
| ForwardedHeaders | Enabled, KnownProxies |
| Features | EnableUserEntriesImport |
| Enrichment | PollIntervalSeconds, BatchSize, MaxRetries |

For local development, the defaults in `appsettings.json` work with the Docker
Compose PostgreSQL setup.
