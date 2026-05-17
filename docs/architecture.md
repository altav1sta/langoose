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
                  ┌──────────────┐
                  │  Corpus DB   │
                  │ (Wiktionary, │
                  │  read-only)  │
                  └──────────────┘
```

The API serves the SPA and handles authentication, dictionary management, and study
sessions. The Worker is a separate process that runs background enrichment — polling
pending items, looking up linguistic data from the read-only corpus database, and
writing enriched content back to the application database.

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
│  │ LocalEnrichmentProvider              │    │
│  │ (corpus provider tracked in #92),    │    │
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
| Worker | `Langoose.Worker` | `UserEntriesImportJob`, `CorpusImportJob` polling loops + DI composition. Dispatches into Core services. Depends on Core + Domain + Data + Corpus.Data. |

Additional projects:
- `Langoose.Auth.Data` — auth DbContext, Identity + OpenIddict persistence. Depends on Domain.
- `Langoose.DbTool` — CLI for applying migrations and seeding in hosted environments. Depends on Data + Domain.

### DTO Mapping

Controllers own all mapping between request/response DTOs and domain models. Services
never see DTOs — they accept and return domain entity types. This keeps the Core layer
independent of API contract details.

## Data Model

The content model has 2 main entity tables with implicit M2M join tables:

- **DictionaryEntry** — a word or form in any language (base forms and derived forms
  in the same table, linked by `BaseEntryId`). `Translations` navigation links
  base forms across languages (source → target, implicit M2M).
- **EntryContext** — a learning context (sentence + cloze gap) linked to a specific
  form. `Translations` navigation links paired contexts across languages
  (implicit M2M).

User-specific tables: **UserEntry** (per-user entries with enrichment
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
- **Staging**: Vercel (web) + Coolify (API and Worker as separate apps) + Neon
  (app and auth PostgreSQL). The corpus PostgreSQL is self-hosted alongside the
  apps on the same Coolify instance so it can stay on a private network and
  hold larger datasets without paying for managed-DB storage. Vercel proxies
  `/api/*` to the API for same-origin cookies. Both API and Worker expose
  `/health` for Coolify's HTTP liveness probes.
- **Production**: not yet determined (M4 scope).
