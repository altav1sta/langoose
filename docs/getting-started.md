# Getting Started

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) and npm
- [Docker](https://www.docker.com/) and Docker Compose (for PostgreSQL)
- [Git](https://git-scm.com/)

## Quick Start

### 1. Start the database

```bash
docker compose up -d postgres
```

This starts PostgreSQL on port 5432 with two databases: `langoose_app` and
`langoose_auth`. Credentials are in `docker-compose.yml`.

### 2. Run the API

```bash
dotnet run --project apps/api/src/Langoose.Api/Langoose.Api.csproj
```

On first run, the API auto-applies migrations and seeds base dictionary content.
The API listens on `http://localhost:5000` by default.

### 3. Run the frontend

```bash
cd apps/web
npm install
npm run dev
```

The frontend runs on `http://localhost:5173` and proxies API calls to port 5000.

### 4. Run the Worker (optional)

```bash
dotnet run --project apps/api/src/Langoose.Worker/Langoose.Worker.csproj
```

The Worker processes background enrichment. Only needed when testing enrichment
features. Requires `Features.EnableAiEnrichment = true` in appsettings.

## Running with Docker Compose

To run everything containerized:

```bash
docker compose up -d postgres
docker compose up -d api --build
docker compose up -d web --build
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
  --startup-project apps/api/src/Langoose.Api
```

Auth database:

```bash
dotnet ef migrations add <MigrationName> \
  --project apps/api/src/Langoose.Auth.Data \
  --startup-project apps/api/src/Langoose.Api
```

Migrations are auto-applied on startup locally. For staging/production, use
`Langoose.DbTool` (see `docs/staging-db-operations.md`).

## Project Structure

See [architecture.md](architecture.md) for the full project map and onion layer
description.

## Configuration

The API reads configuration from `appsettings.json`:

| Section | Key settings |
|---------|-------------|
| ConnectionStrings | AppDatabase, AuthDatabase |
| Cors | AllowedOrigins |
| ForwardedHeaders | Enabled, KnownProxies |
| Features | EnableAiEnrichment |
| Enrichment | PollIntervalSeconds, BatchSize, MaxRetries |
| Gemini | ApiKey, Model, MaxOutputTokens |

For local development, the defaults in `appsettings.json` work with the Docker
Compose PostgreSQL setup.
