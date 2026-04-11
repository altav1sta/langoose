# Langoose EF Core Structure Guidance

## Layout

- `apps/api/src/Langoose.Domain` — entity classes, enums, constants.
- `apps/api/src/Langoose.Data` — `AppDbContext`, entity configurations, migrations, seeding.
- `apps/api/src/Langoose.Auth.Data` — auth `DbContext`, auth configuration, auth migrations.
- `apps/api/src/Langoose.Core` — services that use `AppDbContext` directly.

## Entities

All entities use `Guid` primary keys generated with `Guid.CreateVersion7()` (time-ordered,
better for B-tree indexing in PostgreSQL).

### App Database (AppDbContext)

| Entity | Table | Key Relationships |
|--------|-------|-------------------|
| SharedItem | shared_items | Has many Glosses, ExampleSentences |
| Gloss | glosses | FK → SharedItem. Has many GlossSurfaceForms |
| GlossSurfaceForm | gloss_surface_forms | FK → Gloss |
| ExampleSentence | example_sentences | FK → SharedItem |
| UserItem | user_items | FK → SharedItem (nullable) |
| UserCustomSentence | user_custom_sentences | FK → UserItem |
| UserProgress | user_progress | FK → SharedItem. Unique (UserId, SharedItemId) |
| StudyEvent | study_events | FK → SharedItem, FK → ExampleSentence (nullable) |
| ContentFlag | content_flags | FK → SharedItem |
| ImportRecord | import_records | No FKs to content tables |

### Auth Database (AuthDbContext)

Unchanged. Contains `AuthUser`, `AuthSession`, and OpenIddict tables.

## Conventions

- One `IEntityTypeConfiguration<T>` per entity in `Data/Configurations/`.
- Use `ApplyConfigurationsFromAssembly` in `AppDbContext.OnModelCreating`.
- Enum fields stored as strings via `HasConversion<string>()`.
- PostgreSQL arrays (`text[]`) for `List<string>` properties (Tags, etc.).
- Services use `AppDbContext` directly — no repository-per-entity abstraction.
- Keep EF-specific concerns out of controllers. Controllers call services.

## Key Indexes

- `SharedItem`: index on `NormalizedText`.
- `Gloss`: unique on `(SharedItemId, Language, CanonicalForm)`.
- `GlossSurfaceForm`: unique on `(GlossId, SurfaceForm)`, index on `SurfaceForm` for fast lookup.
- `UserItem`: index on `UserId`, index on `(EnrichmentStatus)` for worker polling.
- `UserProgress`: unique on `(UserId, SharedItemId)`.
- `ExampleSentence`: index on `SharedItemId`.

## Migrations

- No production data yet — fresh migrations are acceptable for major model reworks.
- Delete old migration files and create a new `InitialFoundation` migration.
- App migrations: `dotnet ef migrations add <Name> --project apps/api/src/Langoose.Data --startup-project apps/api/src/Langoose.Api`
- Auth migrations: `dotnet ef migrations add <Name> --project apps/api/src/Langoose.Auth.Data --startup-project apps/api/src/Langoose.Api`
- Local/Docker: auto-applied on startup. Hosted: applied via `Langoose.DbTool`.

## Seeding

- `DatabaseSeeder` and `SeedDataLoader` in `Data/Seeding/`.
- `base-store.json` contains SharedItems with Glosses and ExampleSentences.
- ExampleSentences in seed data include `ExpectedAnswer`, `GrammarHint`,
  `SentenceTranslation`, and `Difficulty`.
- GlossSurfaceForm entries seeded with canonical forms for base items.
- Seeding is empty-database-only via `Langoose.DbTool seed-app`.

## Review Checklist

- Are all Guid PKs using `Guid.CreateVersion7()`?
- Are entity configurations in separate files per entity?
- Are enum fields stored as strings?
- Are indexes appropriate for query patterns (especially GlossSurfaceForm lookup)?
- Are migrations discoverable and runnable?
- Is `Program.cs` still simple?
- Did tests keep exercising business rules rather than EF internals?
