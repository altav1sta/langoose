# Langoose Enrichment Guidance

## Purpose

Enrichment generates learning contexts, translations, grammar labels, difficulty
metadata, and morphological form data for dictionary entries. It is a shared content
layer: AI-generated content lives in `DictionaryEntry` / `EntryContext` and is
reusable across all users. User-provided custom content lives in
`UserDictionaryEntry` / `UserEntryContext` and is private per user.

## Domain Model

### DictionaryEntry

A word or form in any language. Base forms and derived forms live in the same table,
linked by `BaseEntryId`. Only contains validated/enriched content.

- `("en", "book", base, "verb")` — base form
- `("en", "booked", →book, "past simple")` — derived form
- `("ru", "книга", base, "noun")` — base form
- `("ru", "книгу", →книга, "accusative")` — derived form

### EntryTranslation

Links base forms across languages as word-level translation hints. Stored
bidirectionally. Composite PK `(SourceEntryId, TargetEntryId)`.

### EntryContext

A learning context linked to a specific DictionaryEntry form. Contains `Text`
(full sentence) and `Cloze` (sentence with gap). The expected answer and grammar
hint are derived from the linked entry — not stored on the context.

### ContextTranslation

Links paired EntryContexts across languages. Stored bidirectionally. Composite PK.

### UserDictionaryEntry

Per-user custom entry. Owns the enrichment lifecycle:
- `DictionaryEntryId` is nullable — null while pending enrichment
- `EnrichmentStatus`: Pending, Enriched, Failed
- `EnrichmentAttempts` and `EnrichmentNotBefore` for retry backoff

## Provider Interface

`IEnrichmentProvider` in Domain:
```
Task<EnrichmentResult[]> EnrichBatchAsync(EnrichmentRequest[] batch, CancellationToken)
```

Batch-oriented — multiple terms per LLM call.

### Implementations (in Core/Providers)

- `LocalEnrichmentProvider` — static lexicon, sync fallback
- `GeminiEnrichmentProvider` — Gemini Flash REST API, structured JSON response

## Background Worker

`EnrichmentBackgroundService` in the Worker project:
1. Polls UserDictionaryEntries with `EnrichmentStatus = Pending` in batches
2. Dedup check: look up raw translation as a DictionaryEntry form → base → EntryTranslation
3. Calls `IEnrichmentProvider.EnrichBatchAsync()`
4. Creates DictionaryEntry (base + forms), EntryTranslation, EntryContext, ContextTranslation
5. Links UserDictionaryEntry → DictionaryEntry, status → Enriched
6. On failure: increment attempts; mark Failed after MaxRetries

Feature flag `Features.EnableAiEnrichment` controls processing.

## Content Generation

For each term, the LLM produces:
- Base forms and derived forms (both languages) with grammar labels
- EntryTranslation links between base forms (bidirectional)
- 1–3 learning contexts, each with:
  - Source language sentence + cloze (linked to specific source form)
  - Target language sentence (linked to specific target form)
  - ContextTranslation link between paired contexts
  - Per-context difficulty (A1–B2)

## Enrichment Flow

### User adds "book" + "книгу"

1. Look up DictionaryEntry("книгу", ru) → follow BaseEntryId → "книга"
2. Check EntryTranslation for any linked English entry
3. If found → create UserDictionaryEntry linked to it. Done.
4. If not found → create UserDictionaryEntry(Pending). Worker picks it up.

### Worker processes pending item

1. LLM returns forms, translations, contexts
2. Create DictionaryEntries, EntryTranslations, EntryContexts, ContextTranslations
3. Link UserDictionaryEntry → DictionaryEntry, status → Enriched

### Second user adds "book" + "книге"

1. DictionaryEntry("книге", ru) found → base "книга" → EntryTranslation → "book"
2. Linked immediately. No LLM call.

## Rate Limiting

- Per-user throttle: max items per hour/day
- API-level: Gemini free-tier limits, in-memory sliding window
- Backoff: 429/5xx sets `EnrichmentNotBefore` with exponential delay
- Permanent failure after MaxRetries

## Review Checklist

- Does DictionaryEntry only contain validated/enriched content?
- Does UserDictionaryEntry own the pending/failed lifecycle?
- Are derived forms created for dedup lookups?
- Are EntryTranslation and ContextTranslation stored bidirectionally?
- Does the provider use batch-oriented async methods?
- Are external API calls rate-limited?
