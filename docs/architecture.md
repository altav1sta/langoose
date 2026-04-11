# Architecture

Langoose is an English vocabulary learning app for Russian speakers. The backend is
ASP.NET Core with PostgreSQL; the frontend is React + TypeScript + Vite.

## System Overview

```
┌─────────┐       ┌─────────────┐       ┌────────────┐
│  React  │──────▶│  ASP.NET    │──────▶│ PostgreSQL │
│  SPA    │  API  │  Core API   │  EF   │  (app +    │
│         │◀──────│             │  Core │   auth)    │
└─────────┘       └─────────────┘       └────────────┘
                  ┌─────────────┐              │
                  │   Worker    │──────────────┘
                  │  (enrichment│  EF Core
                  │   pipeline) │
                  └─────────────┘
                        │
                        ▼
                  ┌─────────────┐
                  │ Gemini Flash│
                  │   (LLM)    │
                  └─────────────┘
```

The API serves the SPA and handles authentication, dictionary management, and study
sessions. The Worker is a separate process that runs background enrichment — polling
pending items, calling the LLM, and writing enriched content back to the database.
Both share the same PostgreSQL instance.

## Onion Architecture

Dependencies point inward. Domain has no dependencies; everything else depends on it.

```
┌──────────────────────────────────────────────┐
│  Presentation                                │
│  ┌──────────────┐  ┌──────────────────┐      │
│  │     Api      │  │     Worker       │      │
│  │ (controllers,│  │ (background      │      │
│  │  DI, DTOs)   │  │  service, DI)    │      │
│  └──────┬───────┘  └────────┬─────────┘      │
├─────────┼───────────────────┼────────────────┤
│  Core (service implementations, providers)   │
│  ┌──────────────────────────────────────┐    │
│  │ DictionaryService, StudyService,     │    │
│  │ ContentService,                      │    │
│  │ LocalEnrichmentProvider,             │    │
│  │ GeminiEnrichmentProvider,            │    │
│  │ TextNormalizer                       │    │
│  └──────────────────┬───────────────────┘    │
├─────────────────────┼────────────────────────┤
│  Data (EF Core persistence)                  │
│  ┌──────────────────────────────────────┐    │
│  │ AppDbContext, entity configurations, │    │
│  │ migrations, seeding                  │    │
│  └──────────────────┬───────────────────┘    │
├─────────────────────┼────────────────────────┤
│  Domain (innermost — no dependencies)        │
│  ┌──────────────────────────────────────┐    │
│  │ Entities, enums, constants,          │    │
│  │ service interfaces                   │    │
│  └──────────────────────────────────────┘    │
└──────────────────────────────────────────────┘
```

### Layer Responsibilities

| Layer | Project | What lives here |
|-------|---------|-----------------|
| Domain | `Langoose.Domain` | Entities, enums, constants, service interfaces. No dependencies. |
| Data | `Langoose.Data` | AppDbContext, entity configurations, migrations, seeding. Depends on Domain. |
| Core | `Langoose.Core` | Service implementations, enrichment providers, TextNormalizer. Depends on Domain + Data. |
| Api | `Langoose.Api` | Controllers, request/response DTOs, middleware, DI composition. Depends on Core + Domain + Data + Auth.Data. |
| Worker | `Langoose.Worker` | EnrichmentBackgroundService, DI composition. Depends on Core + Domain + Data. |

Additional projects:
- `Langoose.Auth.Data` — auth DbContext, Identity + OpenIddict persistence. Depends on Domain.
- `Langoose.DbTool` — CLI for applying migrations and seeding in hosted environments. Depends on Data + Domain.

### DTO Mapping

Controllers own all mapping between request/response DTOs and domain models. Services
never see DTOs — they accept and return domain entity types. This keeps the Core layer
independent of API contract details.

## Data Model

The content model has 4 main tables plus 2 mapping tables:

- **DictionaryEntry** — a word or form in any language (base forms and derived forms
  in the same table, linked by `BaseEntryId`)
- **EntryTranslation** — links base forms across languages (bidirectional)
- **EntryContext** — a learning context (sentence + cloze gap) linked to a specific form
- **ContextTranslation** — links paired contexts across languages (bidirectional)

User-specific tables: **UserDictionaryEntry** (per-user entries with enrichment
lifecycle), **UserEntryContext** (private learning contexts), **UserProgress**
(spaced repetition state), **StudyEvent** (answer history).

See [domain-model.md](domain-model.md) for the complete entity reference and ER diagram.

## Project Map

```
apps/
  api/
    src/
      Langoose.Domain/        Entities, enums, service interfaces
      Langoose.Core/          Service implementations, providers
      Langoose.Data/          EF Core, migrations, seeding
      Langoose.Api/           Controllers, DTOs, middleware
      Langoose.Worker/        Background enrichment host
      Langoose.Auth.Data/     Auth persistence
      Langoose.DbTool/        Migration/seeding CLI
    tests/
      Langoose.Core.UnitTests/
      Langoose.Api.IntegrationTests/
  web/
    src/                      React + TypeScript SPA
```

## Databases

Two PostgreSQL databases on the same server:
- **langoose_app** — dictionary entries, learning contexts, study progress
- **langoose_auth** — Identity users, sessions, OpenIddict tokens

Separated so auth can be managed independently (different backup cadence,
different migration lifecycle).

## Deployment

- **Local**: `docker compose up` for PostgreSQL, `dotnet run` for API and Worker,
  `npm run dev` for frontend.
- **Staging**: Vercel (web) + Railway (API) + Neon (PostgreSQL). Vercel proxies
  `/api/*` to Railway for same-origin cookies.
- **Production**: not yet determined (M4 scope).

See `docs/staging-*.md` for staging setup details.
