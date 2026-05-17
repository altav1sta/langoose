# EF Core Structure

## Entities

All entities use `Guid` primary keys generated with `Guid.CreateVersion7()` (time-ordered,
better for B-tree indexing in PostgreSQL). Mapping tables use composite primary keys.

### App Database (AppDbContext)

| Entity | Table | Key Relationships |
|--------|-------|-------------------|
| DictionaryEntry | dictionary_entries | Self-ref BaseEntryId, has many EntryContexts, has many Senses |
| Sense | senses | FK -> DictionaryEntry, has many SenseTranslations. Unique (DictionaryEntryId, SenseIndex) |
| SenseTranslation | sense_translations | Composite PK (SourceSenseId, TargetSenseId). Carries Rank |
| EntryContext | entry_contexts | FK -> DictionaryEntry, M2M Translations |
| UserEntry | user_entries | FK SourceEntryId -> DictionaryEntry, FK TargetEntryId -> DictionaryEntry (both nullable) |
| UserEntryContext | user_entry_contexts | FK -> UserEntry |
| UserProgress | user_progress | FK -> DictionaryEntry. Unique (UserId, DictionaryEntryId) |
| StudyEvent | study_events | FK -> DictionaryEntry, FK -> EntryContext (nullable) |
| ContentFlag | content_flags | FK -> DictionaryEntry |
| UserImport | user_imports | No FKs to content tables |
| ImportEntry | import_entries | Pre-promotion holding table for the bulk-seed pipeline. JSONB Payload, status enum, AI fields, optional PromotedEntryId. See dictionary-schema-design.md |
| BackgroundJob | background_jobs | Universal job row for any asynchronous worker-driven task (pipeline stages today, generic async work later). JSONB Settings (per-invocation inputs) and ExecutionState (cursor + counters + error info), both type-specific. See dictionary-schema-design.md |

### Auth Database (AuthDbContext)

Contains `AuthUser`, `AuthSession`, and OpenIddict tables. Unchanged by domain reworks.

## Conventions

- `EFCore.NamingConventions` with `UseSnakeCaseNamingConvention()` in
  `AppDbContext.OnConfiguring` — all table, column, index, and FK names are
  auto-converted to snake_case. No manual `ToTable()` calls needed.
- Connection strings to Neon (and any pooled PostgreSQL provider) must use
  `Channel Binding=Prefer` rather than `Require`. Npgsql 10's channel-binding
  implementation hangs against pooled endpoints under certain TLS session
  conditions, even though the bare TCP+SSL path is fine and libpq works the
  same way. `Prefer` still negotiates channel binding when both sides
  support it, but degrades gracefully when they don't.
- One `IEntityTypeConfiguration<T>` per entity in `Data/Configurations/`.
- Use `ApplyConfigurationsFromAssembly` in `AppDbContext.OnModelCreating`.
- Enum fields stored as strings via `HasConversion<string>()`.
- PostgreSQL arrays (`text[]`) for `List<string>` properties (Tags, etc.).
- Use implicit many-to-many via `HasMany().WithMany().UsingEntity()` when the
  join carries no extra data (e.g. `EntryContext.Translations`). When the join
  needs columns of its own (rank, audit fields), define an explicit join
  entity class — `SenseTranslation` is the canonical example (composite PK on
  `(SourceSenseId, TargetSenseId)`, carries `Rank`).
- Services use `AppDbContext` directly — no repository-per-entity abstraction.
- Keep EF-specific concerns out of controllers.

## Key Indexes

- `DictionaryEntry`: index on `(Language, Text, PartOfSpeech)`, index on `BaseEntryId`.
- `Sense`: unique on `(DictionaryEntryId, SenseIndex)`.
- `SenseTranslation`: composite PK `(SourceSenseId, TargetSenseId)` covers source-leading lookups; explicit index on `TargetSenseId` for the reverse FK and cascade-delete path.
- `EntryContext`: index on `DictionaryEntryId`.
- `UserEntry`: index on `UserId`,
  index on `(EnrichmentStatus, CreatedAtUtc)` for worker polling.
- `UserProgress`: unique on `(UserId, DictionaryEntryId)`.
- `StudyEvent`: index on `(UserId, CreatedAtUtc)` for dashboard queries.
- `ImportEntry`: index on `Status` (worker polling), unique on `(Source, SourceRefId)` (idempotent re-runs via `INSERT ... ON CONFLICT DO NOTHING`).
- `BackgroundJob`: no indexes — table is small (hundreds of rows lifetime). Worker polling does seq-scan over pending rows.

## Migrations

- No production data yet — fresh migrations are acceptable for major model reworks.
- Delete old migration files and create a new `InitialFoundation` migration.
- App migrations: `dotnet ef migrations add <Name> --project apps/api/src/Langoose.Data --startup-project apps/api/src/Langoose.DbTool --context AppDbContext`
- Auth migrations: `dotnet ef migrations add <Name> --project apps/api/src/Langoose.Auth.Data --startup-project apps/api/src/Langoose.DbTool --context AuthDbContext`
- Local/Docker: auto-applied on startup. Hosted: applied via `Langoose.DbTool`.

## Seeding

- `DatabaseSeeder` and `SeedDataLoader` in `Data/Seeding/`.
- `base-store.json` contains DictionaryEntries (base forms + derived forms)
  and EntryContexts. The seeder materializes one default `Sense` per entry
  (`SenseIndex = 0`) and links cross-language pairs as `SenseTranslation`
  rows in both directions.
- Seeding is empty-database-only via `Langoose.DbTool seed-app`.
- The hardcoded JSON seed is a placeholder that will be replaced by the
  bulk-seed pipeline (#57) once that pipeline ships. See
  `dictionary-schema-design.md` for the staged-pipeline design.

## Review Checklist

- Are all Guid PKs using `Guid.CreateVersion7()`?
- Are mapping tables using composite PKs?
- Are entity configurations in separate files per entity?
- Are enum fields stored as strings?
- Are indexes appropriate for query patterns?
- Are migrations discoverable and runnable?
