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

- `LocalEnrichmentProvider` — returns base-form entries, always valid
- `GeminiEnrichmentProvider` — Gemini Flash REST API (future)

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

- Per-user throttle: max items per hour/day
- API-level: Gemini free-tier limits, in-memory sliding window
- Backoff: 429/5xx sets `EnrichmentNotBefore` with exponential delay
- Permanent failure after MaxRetries → ProviderError

## Review Checklist

- Does DictionaryEntry only contain validated/enriched content?
- Does UserDictionaryEntry own the pending/failed lifecycle?
- Are derived forms created for dedup lookups?
- Are Translations linked (source → target direction)?
- Does the provider use batch-oriented async methods?
- Is PartOfSpeech required on both DictionaryEntry and UserDictionaryEntry?
- Are external API calls rate-limited?
