# Enrichment Pipeline

The enrichment pipeline validates user input, generates dictionary entry forms
(base + derived), and links source ↔ target translations.

## How It Works

```
User adds "book" (noun) + "книга"
         │
         ▼
┌──────────────────────────────┐
│  DictionaryService           │
│  1. Find DictionaryEntry     │
│     ("книга", ru, noun)      │
│  2. Follow BaseEntryId       │
│     → ("книга", ru, base)    │
│  3. Check Translations nav   │
│     → any linked en entry?   │
└─────────┬────────────────────┘
          │
     ┌────┴────┐
     │ Found?  │
     └────┬────┘
    yes   │   no
     │    │    │
     ▼    │    ▼
 Set      │  Create UserDictionaryEntry
 Source +  │  (Pending, SourceEntry = null)
 Target    │        │
 Entry     │        │
     │         │
     ▼         ▼
  Done     Worker picks it up
           (background, batched)
                │
                ▼
      ┌──────────────────────┐
      │ IEnrichmentProvider   │
      │ .EnrichBatchAsync()   │
      └────────┬──────────────┘
               │
               ▼
      ┌─────────────────┐
      │  Corpus lookup  │
      │  (or Local      │
      │   fallback)     │
      └────────┬────────┘
               │
               ▼
      Create DictionaryEntries (base + forms)
      Link source → target via Translations nav
      Set SourceEntry + TargetEntry navigations
      Status → Enriched
```

## Provider Architecture

`IEnrichmentProvider` is defined in Domain:

```
Task<EnrichmentResult[]> EnrichBatchAsync(
    UserDictionaryEntry[] batch,
    CancellationToken cancellationToken)
```

The provider receives tracked `UserDictionaryEntry` items directly. It checks
`SourceEntry`/`TargetEntry` navigation nullability to determine what to generate.
POS comes from `UserDictionaryEntry.PartOfSpeech`.

Two implementations in Core:

- **LocalEnrichmentProvider** — returns base-form entries, always valid. Used as
  fallback when corpus enrichment is unavailable.
- **CorpusEnrichmentProvider** (future, tracked in #92) — looks up the corpus
  database (`langoose_corpus`) backed by Wiktionary and supplementary sources.
  Validates terms, generates derived forms, validates translation pairs.

## What the Provider Returns

Each `EnrichmentResult` contains:
- `UserEntryId` — for matching result to item
- `Status` — Enriched, InvalidSource, InvalidTarget, or InvalidLink
- `SourceEntries?` — base + derived forms for source language
- `TargetEntries?` — base + derived forms for target language

Each `EnrichedEntry` contains: Text, IsBaseForm, GrammarLabel?, Difficulty?.
POS is not on the entry — it comes from the user entry.

## Background Worker

`EnrichmentBackgroundService` runs in the Worker project as a thin polling shell.
`EnrichmentProcessor` in Core contains all batch processing logic.

1. **Poll**: query UserDictionaryEntries where status is Pending or ProviderError
   (with attempts < max) and `EnrichmentNotBefore` is null or past.
   Include SourceEntry (with Translations) and TargetEntry.
2. **Resolve**: for items missing navigations, batch-lookup existing entries by
   `EntryKey(Language, Text, PartOfSpeech)`. Set SourceEntry/TargetEntry from
   lookup. Items with both entries and a translation link → set Enriched, skip.
3. **Enrich**: pass remaining items to `IEnrichmentProvider.EnrichBatchAsync()`.
4. **Materialize**: for each successful result:
   - Create base entry + derived entries for missing source side
   - Create base entry + derived entries for missing target side
   - Link source → target via Translations navigation
   - Set Enriched
5. **Handle validation failures**: InvalidSource, InvalidTarget, InvalidLink are
   terminal — no retry.
6. **Handle provider errors**: increment attempts, exponential backoff via
   `EnrichmentNotBefore`. ProviderError after max retries.
7. **Save**: single `SaveChangesAsync` at the end.

The worker creates a new DI scope per poll cycle since `AppDbContext` is scoped.

## Form-Based Deduplication

When the provider enriches a term, it creates DictionaryEntry rows for base forms
and derived forms. Derived forms point back to their base via `BaseEntryId`.

Over time, this builds a lookup index:

```
DictionaryEntry("книгу", ru, noun, base→"книга")
DictionaryEntry("книге", ru, noun, base→"книга")
DictionaryEntry("книга", ru, noun, base=null)
  ← Translations →
DictionaryEntry("book", en, noun, base=null)
```

When a second user adds "book" (noun) + "книге", the lookup finds the existing
DictionaryEntry("книге", ru, noun) → base → Translations → "book" (en).
No provider call needed.

## Rate Limiting

- **Per-user**: maximum items per hour/day (configurable). Prevents abuse.
  Tracked in #77.
- **Provider-side**: corpus lookups have no external API limits. The corpus
  database is local and unmetered.
- **Backoff**: on transient provider errors, the worker sets
  `EnrichmentNotBefore` with exponential delay. Retried later.
- **Permanent failure**: after max retries (configurable, default 3), the item
  is marked `ProviderError`.

## Feature Flag

`EnableAiEnrichment` feature flag (via `Microsoft.FeatureManagement`) controls
whether the Worker processes pending items. Configured in `appsettings.json`:

```json
"feature_management": {
  "feature_flags": [
    { "id": "EnableAiEnrichment", "enabled": false }
  ]
}
```
