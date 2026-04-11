# Enrichment Pipeline

The enrichment pipeline generates study content for dictionary entries: learning
contexts with cloze gaps, sentence translations, grammar labels, difficulty ratings,
and morphological form data.

## How It Works

```
User adds "book" + "книгу"
         │
         ▼
┌──────────────────────────────┐
│  DictionaryService           │
│  1. Find DictionaryEntry     │
│     ("книгу", ru)            │
│  2. Follow BaseEntryId       │
│     → ("книга", ru)          │
│  3. Check EntryTranslation   │
│     → any linked en entry?   │
└─────────┬────────────────────┘
          │
     ┌────┴────┐
     │ Found?  │
     └────┬────┘
    yes   │   no
     │    │    │
     ▼    │    ▼
 Link to  │  Create UserDictionaryEntry
 existing │  (Pending, DictionaryEntryId = null)
 entry    │        │
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
      │  LLM (Gemini)   │
      │  or Local        │
      │  fallback        │
      └────────┬────────┘
               │
               ▼
      Create DictionaryEntries (base + forms)
      Create EntryTranslations (bidirectional)
      Create EntryContexts + ContextTranslations
      Link UserDictionaryEntry → DictionaryEntry
      Status → Enriched
```

## Provider Architecture

`IEnrichmentProvider` is defined in Domain with a batch-oriented interface:

```
Task<EnrichmentResult[]> EnrichBatchAsync(
    EnrichmentRequest[] batch,
    CancellationToken cancellationToken)
```

Two implementations in Core:

- **LocalEnrichmentProvider** — static lexicon with a handful of entries. Used as
  fallback when AI enrichment is disabled or fails.
- **GeminiEnrichmentProvider** — calls Gemini Flash REST API. Sends one batched
  prompt with multiple terms, receives a structured JSON array response.

The Worker resolves `IEnrichmentProvider` from DI. When `Features.EnableAiEnrichment`
is true and the Gemini API key is configured, it uses Gemini; otherwise Local.

## What the LLM Produces

For each term in a batch, the LLM returns:

| Field | Example |
|-------|---------|
| Base form (target language) | "книга" |
| Derived forms | ["книги", "книгу", "книге", "книгой"] with grammar labels |
| Base form (source language) | "book" |
| Derived forms (source) | ["booked", "books", "booking"] with grammar labels |
| Part of speech | "noun" |
| General difficulty | "A1" |
| Learning contexts (1–3) | See below |

Each learning context includes a bilingual sentence pair:

| Field | Example |
|-------|---------|
| Source sentence | "She bought a new book yesterday." |
| Source cloze | "She bought a new ____ yesterday." |
| Source form | "book" (links to the specific DictionaryEntry form) |
| Target sentence | "Она купила новую книгу вчера." |
| Target form | "книгу" (links to the specific DictionaryEntry form) |
| Difficulty | "A1" |

The expected answer is the source form's Text. The grammar hint is the source
form's GrammarLabel. Neither is stored on the context — both are derived from
the linked DictionaryEntry.

## Background Worker

`EnrichmentBackgroundService` runs in the Worker project:

1. **Poll**: query UserDictionaryEntries where `EnrichmentStatus = Pending` and
   `EnrichmentNotBefore` is null or in the past. Order by `CreatedAtUtc`.
   Limit to batch size (configurable, default 10).
2. **Deduplicate**: for each item, check if a matching DictionaryEntry already
   exists by looking up the raw translation text as a form. If found, skip
   enrichment and link directly.
3. **Enrich**: call `IEnrichmentProvider.EnrichBatchAsync()` with remaining items.
4. **Persist**: for each successful result:
   - Create DictionaryEntry base forms + derived forms (both languages)
   - Create EntryTranslation links (bidirectional)
   - Create EntryContext for each learning context (linked to the specific form)
   - Create paired EntryContext in the target language
   - Create ContextTranslation links (bidirectional)
   - Set `UserDictionaryEntry.DictionaryEntryId`, status to Enriched
5. **Handle failures**: increment `EnrichmentAttempts`. If over max retries, mark
   Failed. On rate-limit responses (HTTP 429), set `EnrichmentNotBefore` with
   exponential backoff.
6. **Sleep**: wait for the configured poll interval (default 5 seconds), then repeat.

The worker creates a new DI scope per poll cycle since `AppDbContext` is scoped.

## Form-Based Deduplication

When the LLM enriches a term, it creates DictionaryEntry rows for base forms and
derived forms in both languages. Derived forms point back to their base via
`BaseEntryId`.

Over time, this builds a lookup index:

```
DictionaryEntry("книгу", ru, base→"книга")
DictionaryEntry("книге", ru, base→"книга")
DictionaryEntry("книга", ru, base=null)
  ← EntryTranslation →
DictionaryEntry("book", en, base=null)
```

When a second user adds "book" + "книге", the lookup finds the existing
DictionaryEntry("книге", ru) → base → EntryTranslation → "book" (en).
No LLM call needed.

## Rate Limiting

- **Per-user**: maximum items per hour/day (configurable). Prevents abuse.
- **API-level**: Gemini free-tier limits (requests per minute/day). In-memory
  sliding window in the background worker.
- **Backoff**: on HTTP 429 or 5xx from Gemini, the worker sets
  `UserDictionaryEntry.EnrichmentNotBefore` with exponential delay. Retried
  later, not counted as a permanent failure.
- **Permanent failure**: after max retries (configurable, default 3), the item
  is marked `Failed`.

## Feature Flag

`Features.EnableAiEnrichment` in `appsettings.json` controls whether the Worker
processes pending items. When disabled, the sync enrichment path (using
LocalEnrichmentProvider) is used for base item seeding. User-added items stay
Pending until the flag is enabled.
