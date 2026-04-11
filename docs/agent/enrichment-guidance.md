# Langoose Enrichment Guidance

## Purpose

Enrichment is the process of generating example sentences, translation hints, accepted variants,
and difficulty metadata for dictionary items. It is a shared content layer: AI-generated enrichment
is reusable across all users, while user-provided custom context is private per user.

## Architecture Decisions

- Enrichment runs as an async background process after a word is added.
- Shared enrichment: if an item with the same normalized English text is already enriched, reuse that result.
- User custom context (own sentences, translations) takes priority over AI-generated content and remains private.
- Provider: best available free-tier LLM (currently Gemini Flash). Abstracted behind an interface to allow swapping.
- Batch processing for CSV imports and bulk operations, respecting external API rate limits.

## Enrichment States

Each dictionary item has one of three enrichment states that determine study card eligibility:

1. **Custom context provided** — the user supplied their own sentences/translations. Ready for study.
2. **AI-enriched** — background enrichment completed successfully. Ready for study.
3. **Pending enrichment** — no custom context and not yet AI-enriched. Hidden from study cards.

The frontend should indicate the current enrichment state to the user.

## Content Generation

For each item, enrichment should produce:

- Natural example sentences with cloze gaps (multiple per item when possible)
- Russian translation hints for each sentence
- Accepted answer variants and common collocations
- Difficulty inference (A1 through B2)
- Part of speech when determinable

## Rate Limiting and Cost Control

- All external API calls go through a centralized enrichment queue.
- Processing stays within the provider's free-tier limits.
- Per-user rate limiting prevents abuse (excessive additions in short periods).
- Enrichment failures are retried with backoff; items remain in pending state until success.

## Review Checklist

- Does new enrichment content follow the shared-then-private layering?
- Does the enrichment state correctly gate study card visibility?
- Are external API calls going through the queue with rate limiting?
- Does CSV import trigger batch enrichment without exceeding API limits?
- Is the enrichment provider abstracted behind an interface?
