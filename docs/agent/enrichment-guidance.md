# Langoose Enrichment Guidance

## Purpose

Enrichment validates user input, generates dictionary entry forms, and links
translations. It is a shared content layer: AI-generated content lives in
`DictionaryEntry` / `EntryContext` and is reusable across all users.
User-provided custom content lives in `UserDictionaryEntry` and is private
per user.

## Domain Model

### DictionaryEntry

A word or form in any language. `PartOfSpeech` is required. Base forms and
derived forms live in the same table, linked by `BaseEntryId`. Base forms
have `BaseEntryId = null`.

`Translations` navigation (implicit M2M) links base forms across languages.
Join table: `dictionary_entries_translations (source_id, target_id)`.

### EntryContext

A learning context linked to a specific DictionaryEntry form. Contains `Text`
(full sentence) and `Cloze` (sentence with gap).

`Translations` navigation (implicit M2M) links paired EntryContexts across
languages. Join table: `entry_contexts_translations (source_id, target_id)`.

### UserDictionaryEntry

Per-user custom entry. Owns the enrichment lifecycle:
- `SourceEntryId` and `TargetEntryId` — nullable FKs to source/target base forms
- `PartOfSpeech` — required, set by user on input
- `EnrichmentStatus`: Pending, Enriched, InvalidSource, InvalidTarget, InvalidLink, ProviderError
- `EnrichmentAttempts` and `EnrichmentNotBefore` for retry backoff

### EnrichmentStatus Values

| Status | Terminal? | Retryable? | Meaning |
|--------|-----------|------------|---------|
| Pending | No | N/A | Not yet processed |
| Enriched | Yes | N/A | Fully linked, ready for study |
| InvalidSource | Yes | No | Enricher rejected the source term |
| InvalidTarget | Yes | No | Enricher rejected the target term |
| InvalidLink | Yes | No | Both valid but no semantic relation |
| ProviderError | Yes | Yes (up to MaxRetries) | Transient provider failure |

## Provider Interface

`IEnrichmentProvider` in Domain:
```
Task<EnrichmentResult[]> EnrichBatchAsync(UserDictionaryEntry[] batch, CancellationToken)
```

The provider receives tracked `UserDictionaryEntry` items directly. It checks
`SourceEntry`/`TargetEntry` navigation nullability to determine what to generate.
No separate request DTO — the user entry has all needed context.

Each `EnrichmentResult` returns:
- `UserEntryId` — matches item.Id for result mapping
- `Status` — an `EnrichmentStatus` value
- `SourceEntries?`, `TargetEntries?` — arrays of `EnrichedEntry` (base + derived forms)

`EnrichedEntry` carries: `Text`, `IsBaseForm`, `GrammarLabel?`, `Difficulty?`.
POS is not on the entry — it comes from `UserDictionaryEntry.PartOfSpeech`.

### Implementations (in Core/Providers)

- `LocalEnrichmentProvider` — returns base-form entries, always valid. Used as
  a dev-mode fallback before the corpus pipeline lands.
- (future) `CorpusEnrichmentProvider` — looks up the corpus database (#92) to
  validate inputs, generate inflected forms, and resolve translations. Same
  contract as `LocalEnrichmentProvider`. Tracked under #92.

## Corpus database (langoose_corpus)

A separate, read-only Postgres database providing reference linguistic data
that powers the corpus-based provider. Schema and importer live in:

- `apps/api/src/Langoose.Corpus.Data/` — embedded SQL schema files, POCOs,
  Dapper-based query layer (no EF, no migrations).
- `apps/api/src/Langoose.Corpus.DbTool/` — CLI for `init` and
  `import-wiktionary` (and future importers).

### Source-first schema

Each corpus source gets its own tables that mirror its native shape rather
than fitting into a unified schema. Wiktionary uses a hybrid approach:
structured columns (`lang_code`, `word`, `pos`) for fast indexed lookups
plus a `data JSONB` column preserving the source entry as-is (`forms[]`,
`senses[]`, `translations[]`, `etymology_number`, `sounds`, `categories`,
…). The structured columns duplicate three source fields — a conscious
trade-off: ~2-3% extra JSONB storage in exchange for query ergonomics,
better planner statistics on the hot lookup path, and a faster import
(`data` is the raw source line, written through COPY BINARY with no
per-row re-serialisation).

Concrete consequences of "source-first":

- **No filtering at import**. The importer does not drop entries by POS
  (proper nouns, abbreviations, etc.) — it stores every entry the source
  emits. Filtering happens at query time so the allow-list can change
  without a re-import.
- **Raw values, no normalisation**. The structured `pos` column stores
  Kaikki's raw POS string (e.g. `adj`, not `adjective`). Mapping to a
  canonical vocabulary is a query-time concern.
- **Etymology splits preserved as separate rows**. `wiktionary_entries`
  has no UNIQUE / PK on `(lang_code, word, pos, source_version)` because
  Wiktionary publishes one entry per etymology: English `lead` (noun) is
  two entries — the metal and the leash — with distinct `senses[]` and
  `translations[]` in their JSONB documents. Lookups by `(lang_code,
  word, pos)` may return multiple rows, and the provider merges/picks
  across them at query time rather than the schema forcing a lossy
  collapse at import time.
- **Form lookups go through JSONB containment, not a dedicated form
  table**. To resolve an inflected form (`бронировал`) to its lemma
  (`бронировать`), query with `data @> '{"forms":[{"form":"бронировал"}]}'::jsonb`
  — indexed by the GIN on `data`. A materialised form-index table is
  pure derived data; if benchmarks ever show containment as too slow, it
  can be built as a follow-up step without re-importing the source.

Invariant for query authors: **always filter by `lang_code`**. Every
index on the table is scoped by language; every partitioning scheme we'd
adopt later (#96 territory) is keyed by language. Queries that omit
`lang_code` will full-scan and will break once we partition.

Future sources will add their own tables without modifying existing ones:
- `wordfreq_rankings` (flat) — frequency ranks per word
- `cefr_levels` (flat) — CEFR difficulty per word
- `tatoeba_sentences` + `tatoeba_links` (hybrid) — example sentences for
  context generation (#91)

### JSON serialisation

Corpus document records (`WiktionaryEntry`, `WiktionaryForm`, etc.) use
System.Text.Json source generation via `CorpusJsonContext`. Property names
map to the source's snake_case fields automatically — no per-property
attributes. Add new types to `CorpusJsonContext` with `[JsonSerializable]`
when introducing them.

### Distribution

For development the DbTool runs `import-wiktionary --lang en --source <jsonl>`
against a Kaikki extract. For production a pre-built `pg_dump` artifact is
restored — much faster than re-importing JSONL on every deploy.

Source files (Kaikki JSONL, Tatoeba TSV) are downloaded out of band by
maintainer scripts under `scripts/`, never fetched at deploy time.

### Attribution

Wiktionary data is licensed CC-BY-SA 4.0. Future source additions
(Tatoeba CC-BY 2.0, CEFR-J open) carry their own attribution
requirements. The canonical list of every external data source shipped
or evaluated by the project lives in
[`ATTRIBUTION.md`](../../ATTRIBUTION.md) at the repo root — add new
sources there when their import code lands. Two distinct obligations:

- **Redistribution.** Any dump artifact published via
  `scripts/publish-{full,mini}-corpus-dump.sh` must carry the attribution
  file; the scripts upload `ATTRIBUTION.md` alongside the `.dump` asset
  and cite the sources in the release body.
- **UI surfacing.** The web UI must render a visible attribution notice
  wherever Wiktionary-derived content (translations, forms, example
  sentences) is shown to end users. Tracked under epic #92.

## Background Worker

Two classes split hosting from business logic:

`EnrichmentBackgroundService` (`Worker/Services/`) is a thin polling shell that
extends `BackgroundService`. It checks the feature flag, creates a scope, and
delegates to `IEnrichmentProcessor`.

`EnrichmentProcessor` (`Core/Services/`) implements `IEnrichmentProcessor`
(defined in Domain) and contains all batch processing logic.

Poll cycle:
1. Check feature flag via `IVariantFeatureManager.IsEnabledAsync("EnableAiEnrichment")`
2. `EnrichmentProcessor.ProcessPendingBatchAsync()`:
   a. Query pending items with `Include(SourceEntry.Translations, TargetEntry)`
   b. Load missing entries by `EntryKey(lang, text, POS)` from DB
   c. Set SourceEntry/TargetEntry navigations from lookup
   d. Items with both entries and link → set Enriched, skip provider
   e. Pass remaining items to provider (it reads navigations to know what to generate)
   f. Create base + derived entries from results, link via Translations navigation
   g. Terminal statuses (InvalidSource/InvalidTarget/InvalidLink) set directly
   h. On provider exception: exponential backoff, ProviderError after max retries
   i. Single `SaveChangesAsync` at the end

### Feature Management

Uses `Microsoft.FeatureManagement` NuGet package (v4.4+) with the standard
`feature_management.feature_flags` JSON schema in appsettings.json:

```json
"feature_management": {
  "feature_flags": [
    { "id": "EnableAiEnrichment", "enabled": false }
  ]
}
```

### Configuration

`EnrichmentSettings` (`Core/Configuration/`) bound from the `"Enrichment"` section:
- `PollIntervalSeconds` (default 5)
- `BatchSize` (default 10)
- `MaxRetries` (default 3)

## Content Generation

For each term, the provider produces:
- Base forms and derived forms with POS, grammar labels, difficulty
- Context generation is a separate future job (not part of enrichment)

## Enrichment Flow

### User adds "book" (noun) + "книга"

1. Look up DictionaryEntry("книга", ru, noun) → check Translations for English link
2. If found → set SourceEntryId + TargetEntryId, status Enriched. Done.
3. If not found → create UserDictionaryEntry(Pending). Worker picks it up.

### Worker processes pending item

1. Provider validates "book" (noun) → returns base + derived forms for English
2. Provider validates "книга" (noun) → returns base + derived forms for Russian
3. Provider validates link → confirms "book" and "книга" are semantically related
4. Processor creates missing DictionaryEntries, links base forms via Translations
5. Sets SourceEntryId + TargetEntryId, status → Enriched

## Rate Limiting

- Per-user throttle: max items per hour/day (#77)
- Provider-side: corpus lookups have no external API limits; the corpus is
  local and unmetered
- Backoff: transient provider exceptions set `EnrichmentNotBefore` with
  exponential delay
- Permanent failure after MaxRetries → ProviderError

## Review Checklist

- Does DictionaryEntry only contain validated/enriched content?
- Does UserDictionaryEntry own the pending/failed lifecycle?
- Are derived forms created for dedup lookups?
- Are Translations linked (source → target direction)?
- Does the provider use batch-oriented async methods?
- Is PartOfSpeech required on both DictionaryEntry and UserDictionaryEntry?
- Are external API calls rate-limited?
