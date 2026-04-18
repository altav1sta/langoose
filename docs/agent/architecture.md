# Langoose Architecture

## Principle

Use onion architecture. Dependencies point inward: Domain has no dependencies,
everything else depends on Domain directly or transitively.

```mermaid
flowchart LR
  Api["Langoose.Api"] --> Core["Langoose.Core"]
  Api --> Domain["Langoose.Domain"]
  Api --> Data["Langoose.Data"]
  Api --> AuthData["Langoose.Auth.Data"]
  Worker["Langoose.Worker"] --> Core
  Worker --> Domain
  Worker --> Data
  Core --> Domain
  Core --> Data
  Data --> Domain
  AuthData --> Domain
  DbTool["Langoose.DbTool"] --> Data
  DbTool --> Domain
  CorpusData["Langoose.Corpus.Data"]
  CorpusDbTool["Langoose.Corpus.DbTool"] --> CorpusData
  UnitTests["Core.UnitTests"] --> Core
  UnitTests --> Domain
  IntTests["Api.IntegrationTests"] --> Api
  IntTests --> Data
  IntTests --> Domain
  CorpusIntTests["Corpus.IntegrationTests"] --> CorpusDbTool
  CorpusIntTests --> CorpusData
```

## Solution Layout

`apps/api/Langoose.sln` organises projects with two conventions:

- **Source projects** sit at the root of the solution (no `src` solution
  folder), even though they live on disk under `apps/api/src/`.
- **Test projects** are nested under a `tests` solution folder
  (`NestedProjects` section), matching their on-disk location under
  `apps/api/tests/`.

When adding a new project, do not let `dotnet sln add` create a default
`src` solution folder — it must be removed and the new test project must be
added to the `NestedProjects` section under the existing `tests` folder.

## Layers

### Domain (innermost)

`apps/api/src/Langoose.Domain/` — no dependencies.

Contains:
- **Entities**: `DictionaryEntry`, `EntryContext`, `UserDictionaryEntry`,
  `UserProgress`, `StudyEvent`, `ContentFlag`, `ImportRecord`
- **Enums**: `EnrichmentStatus`, `StudyVerdict`, `FeedbackCode`
- **Constants**: `ProgressDefaults`
- **Service interfaces**: `IDictionaryService`, `IStudyService`, `IContentService`

Service interfaces use only domain model types — no DTOs.

### Data

`apps/api/src/Langoose.Data/` — depends on Domain.

Contains:
- `AppDbContext` with DbSets for all domain entities
- One `IEntityTypeConfiguration<T>` per entity in `Configurations/`
- Migrations (fresh per major model rework, auto-applied on startup locally)
- Seeding (`DatabaseSeeder`, `SeedDataLoader`, `base-store.json`)

### Core

`apps/api/src/Langoose.Core/` — depends on Domain and Data.

Contains:
- **Services**: `DictionaryService`, `StudyService`, `ContentService`
  — implement interfaces from Domain
- **Providers**: `LocalEnrichmentProvider` — implements `IEnrichmentProvider`
  from Domain. The corpus-based provider is tracked under #92.
- **Utilities**: `TextNormalizer` — static utility, no interface
- **Configuration**: settings classes (`EnrichmentSettings`, `FeaturesSettings`)

Services accept and return domain models. They use `AppDbContext` directly — no
repository-per-entity abstraction.

### Api (presentation)

`apps/api/src/Langoose.Api/` — depends on Core, Domain, Data, Auth.Data.

Contains:
- **Controllers**: thin, map request DTOs → domain models → call service → map
  result → response DTOs
- **Models**: request/response DTOs (only auth DTOs and API-specific shapes)
- **Configuration**: `CorsSettings`, `ForwardedHeadersSettings`
- **Middleware**: `AntiforgeryValidationMiddleware`
- `Program.cs`: DI composition root

Controllers own all DTO ↔ domain model mapping. Services never see DTOs.

### Worker (presentation)

`apps/api/src/Langoose.Worker/` — depends on Core, Domain, Data.

Contains:
- **Services**: `EnrichmentBackgroundService` — polls pending items, enriches in
  batches via `IEnrichmentProvider`
- `Program.cs`: generic host DI composition root
- Own `appsettings.json`

Runs as a separate process. Shares the same database.

### Auth.Data

`apps/api/src/Langoose.Auth.Data/` — depends on Domain. Unchanged by this rework.

### DbTool

`apps/api/src/Langoose.DbTool/` — depends on Data, Domain.
CLI for applying migrations and seeding in hosted environments.

### Corpus.Data

`apps/api/src/Langoose.Corpus.Data/` — no project references; uses Dapper +
Npgsql directly. Read-only access layer for the `langoose_corpus` database.
Schema is defined in embedded SQL files (no EF migrations). Hybrid
Postgres + JSONB tables preserve each source's native shape.

### Corpus.DbTool

`apps/api/src/Langoose.Corpus.DbTool/` — depends on Corpus.Data.
CLI for initialising the corpus database schema and importing source
files (Kaikki Wiktionary JSONL, etc.) via streaming + bulk COPY.

## Anti-Goals

- Avoid repository-per-entity by default.
- Avoid mediator or CQRS by default.
- Avoid splitting logic into thin pass-through layers without a strong reason.
- Avoid putting DTOs in Domain — they belong in the presentation layer.
- Avoid making the code harder to trace than the product complexity requires.
