# Langoose Enrichment Guidance

## Purpose

Enrichment generates example sentences, translations, difficulty metadata, grammar hints,
and gloss normalization data for dictionary entries. It is a shared content layer:
AI-generated content lives in `SharedItem` and is reusable across all users. User-provided
custom context lives in `UserItem` / `UserCustomSentence` and is private per user.

## Domain Model

### SharedItem

The English-word-concept table. Only contains validated/enriched content — never pending
or failed entries. Keyed by `NormalizedText`. Has `IsPublic` flag (true for curated base
items, false for user-contributed until admin validation). Has `Source` (Base or
UserContributed).

### Gloss

Links a SharedItem to a canonical translation in a target language. One SharedItem can
have multiple glosses per language (e.g., "book" → "книга" as noun, "забронировать" as
verb are separate SharedItems, each with their own Gloss). Language-agnostic — the
`Language` field supports future expansion beyond Russian.

### GlossSurfaceForm

Maps morphological variants to a canonical Gloss. When a user types "книгу", the lookup
path is: GlossSurfaceForm("книгу") → Gloss("книга") → SharedItem. Populated by the LLM
during enrichment. Grows over time as new forms are encountered.

### ExampleSentence

The actual study material. Each sentence is a complete learning context:
- `SentenceText` / `ClozeText` — the English sentence with and without the gap
- `ExpectedAnswer` — the exact form required by this sentence (e.g., "books", "booked")
- `GrammarHint` — what form the gap expects ("infinitive", "past simple", "plural", etc.)
- `SentenceTranslation` — full sentence translation in the target language
- `Difficulty` — per-sentence, not per-word (same word can be A1 in a simple sentence,
  B2 in an academic context)
- `Language` — target language for translation
- `Origin` — Dataset or Ai

### UserItem

Per-user custom dictionary entry. Owns the enrichment lifecycle:
- `SharedItemId` is nullable — null while pending enrichment
- `EnrichmentStatus`: Pending, Enriched, Failed
- `EnrichmentAttempts` and `EnrichmentNotBefore` for retry backoff
- Preserves `RawEnglishText` and `RawGloss` (what the user typed)

When enrichment succeeds, a SharedItem is created (or an existing one is found),
and UserItem.SharedItemId is set. When enrichment fails after max retries, the
UserItem is marked Failed.

### UserCustomSentence

Private per-user examples with the same fields as ExampleSentence. Linked to UserItem,
not SharedItem.

## Architecture

### Provider Interface

`IEnrichmentProvider` in Domain defines:
```
Task<EnrichmentResult[]> EnrichBatchAsync(EnrichmentRequest[] batch, CancellationToken)
```

Batch-oriented by design — multiple terms per LLM call.

### Implementations (in Core/Providers)

- `LocalEnrichmentProvider` — static lexicon, sync fallback, used when AI is off
- `GeminiEnrichmentProvider` — Gemini Flash REST API, structured JSON response

### Background Worker

`EnrichmentBackgroundService` in the Worker project:
1. Polls `UserItems` with `EnrichmentStatus = Pending` in configurable batches
2. Checks GlossSurfaceForm for existing matches (skip already-enriched concepts)
3. Calls `IEnrichmentProvider.EnrichBatchAsync()`
4. Creates SharedItem + Gloss + GlossSurfaceForm + ExampleSentences
5. Links UserItem.SharedItemId, sets EnrichmentStatus → Enriched
6. On failure: increments EnrichmentAttempts; marks Failed after MaxRetries

Feature flag `Features.EnableAiEnrichment` controls whether the worker processes jobs.

## Content Generation

For each term, the LLM produces:
- Canonical gloss (lemma form) for the target language
- Surface form variants (morphological forms of the gloss)
- 1–3 example sentences, each with:
  - English sentence containing the target term
  - Cloze version with `____` replacing the term
  - `ExpectedAnswer` — the exact form used in this sentence
  - `GrammarHint` — what grammatical form the gap expects
  - `SentenceTranslation` — full sentence translation
  - `Difficulty` — per-sentence difficulty level (A1–B2)
- Part of speech
- General difficulty for the SharedItem (derived from sentence difficulties)

## Enrichment Flow

### User adds "book" + "книгу"

1. Normalize text → "book"
2. Look up GlossSurfaceForm for "книгу" in language "ru"
3. If found → Gloss → SharedItem → create UserItem(SharedItemId=found, Enriched). Done.
4. If not found → create UserItem(SharedItemId=null, Pending). Worker picks it up.

### Worker processes pending UserItem

1. Call LLM with RawEnglishText + RawGloss
2. LLM returns canonical gloss "книга", surface forms ["книги", "книгу", "книге", "книгой"],
   example sentences, part of speech, difficulty
3. Find or create SharedItem by (NormalizedText, PartOfSpeech)
4. Create Gloss("книга") + GlossSurfaceForm entries for all surface forms
5. Create ExampleSentences with ExpectedAnswer, GrammarHint, SentenceTranslation, Difficulty
6. Set UserItem.SharedItemId, EnrichmentStatus → Enriched

### Second user adds "book" + "книге"

1. GlossSurfaceForm("книге") → Gloss("книга") → SharedItem already exists
2. Create UserItem linked to existing SharedItem. Enriched immediately. No LLM call.

## Rate Limiting

- Per-user throttle: max items per hour/day (configurable). Prevents abuse.
- API-level: Gemini free-tier limits (requests per minute/day). In-memory sliding window
  in the background worker.
- On 429/5xx from Gemini: exponential backoff via `UserItem.EnrichmentNotBefore`.
  Not counted as a failure.
- Permanent failures (bad input, repeated LLM errors) marked Failed after MaxRetries.

## Review Checklist

- Does SharedItem only contain validated/enriched content?
- Does UserItem own the pending/failed lifecycle?
- Are GlossSurfaceForm entries created during enrichment for dedup?
- Does the enrichment provider use batch-oriented async methods?
- Are external API calls rate-limited (per-user and API-level)?
- Does CSV import create Pending UserItems for batch processing?
- Is the provider abstracted behind IEnrichmentProvider?
- Do ExampleSentences have ExpectedAnswer, GrammarHint, SentenceTranslation, and Difficulty?
