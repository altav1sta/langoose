# EF Core Structure

## Entities

All entities use `Guid` primary keys generated with `Guid.CreateVersion7()` (time-ordered,
better for B-tree indexing in PostgreSQL). Mapping tables use composite primary keys.

### App Database (AppDbContext)

| Entity | Table | Key Relationships |
|--------|-------|-------------------|
| DictionaryEntry | dictionary_entries | Self-ref BaseEntryId, has many EntryContexts, M2M Translations |
| EntryContext | entry_contexts | FK -> DictionaryEntry, M2M Translations |
| UserDictionaryEntry | user_dictionary_entries | FK -> DictionaryEntry (nullable) |
| UserEntryContext | user_entry_contexts | FK -> UserDictionaryEntry |
| UserProgress | user_progress | FK -> DictionaryEntry. Unique (UserId, DictionaryEntryId) |
| StudyEvent | study_events | FK -> DictionaryEntry, FK -> EntryContext (nullable) |
| ContentFlag | content_flags | FK -> DictionaryEntry |
| ImportRecord | import_records | No FKs to content tables |

### Auth Database (AuthDbContext)

Contains `AuthUser`, `AuthSession`, and OpenIddict tables. Unchanged by domain reworks.

## Conventions

- `EFCore.NamingConventions` with `UseSnakeCaseNamingConvention()` in
  `AppDbContext.OnConfiguring` — all table, column, index, and FK names are
  auto-converted to snake_case. No manual `ToTable()` calls needed.
- One `IEntityTypeConfiguration<T>` per entity in `Data/Configurations/`.
- Use `ApplyConfigurationsFromAssembly` in `AppDbContext.OnModelCreating`.
- Enum fields stored as strings via `HasConversion<string>()`.
- PostgreSQL arrays (`text[]`) for `List<string>` properties (Tags, etc.).
- Implicit many-to-many via `HasMany().WithMany().UsingEntity()` for translation
  links. No explicit join entity classes — EF manages the join tables.
- Services use `AppDbContext` directly — no repository-per-entity abstraction.
- Keep EF-specific concerns out of controllers.

## Key Indexes

- `DictionaryEntry`: index on `(Language, Text)`, index on `BaseEntryId`.
- `EntryContext`: index on `DictionaryEntryId`.
- `UserDictionaryEntry`: index on `(UserId, DictionaryEntryId)`,
  index on `(EnrichmentStatus, CreatedAtUtc)` for worker polling.
- `UserProgress`: unique on `(UserId, DictionaryEntryId)`.
- `StudyEvent`: index on `(UserId, CreatedAtUtc)` for dashboard queries.

## Migrations

- No production data yet — fresh migrations are acceptable for major model reworks.
- Delete old migration files and create a new `InitialFoundation` migration.
- App migrations: `dotnet ef migrations add <Name> --project apps/api/src/Langoose.Data --startup-project apps/api/src/Langoose.DbTool --context AppDbContext`
- Auth migrations: `dotnet ef migrations add <Name> --project apps/api/src/Langoose.Auth.Data --startup-project apps/api/src/Langoose.DbTool --context AuthDbContext`
- Local/Docker: auto-applied on startup. Hosted: applied via `Langoose.DbTool`.

## Seeding

- `DatabaseSeeder` and `SeedDataLoader` in `Data/Seeding/`.
- `base-store.json` contains DictionaryEntries (base forms + derived forms)
  and EntryContexts. Translations use implicit M2M via navigation properties.
- Seeding is empty-database-only via `Langoose.DbTool seed-app`.

## Review Checklist

- Are all Guid PKs using `Guid.CreateVersion7()`?
- Are mapping tables using composite PKs?
- Are entity configurations in separate files per entity?
- Are enum fields stored as strings?
- Are indexes appropriate for query patterns?
- Are migrations discoverable and runnable?
