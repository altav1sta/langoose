# Langoose Dictionary Rules

## Main Files

- `apps/api/src/Langoose.Core/Services/DictionaryService.cs`
- `apps/api/src/Langoose.Api/Controllers/DictionaryController.cs`
- `apps/api/tests/Langoose.Core.UnitTests`
- `apps/api/tests/Langoose.Api.IntegrationTests`
- `apps/web/src/api.ts`

## Data Model

The dictionary uses a two-layer model:

- **DictionaryEntry** — a word or form in any language. Base forms (lemmas) and
  derived forms (inflections, cases) in the same table, linked by `BaseEntryId`.
  Only validated/enriched content. `IsPublic` controls visibility.
- **EntryTranslation** — links base forms across languages (bidirectional).
- **UserDictionaryEntry** — per-user custom entries referencing a DictionaryEntry.
  `DictionaryEntryId` is nullable (null while pending enrichment).

## Visibility Rules

- Users see all public DictionaryEntries (base dictionary) plus their own
  enriched UserDictionaryEntries (DictionaryEntryId is not null).
- Pending UserDictionaryEntries (DictionaryEntryId is null) are visible in the
  dictionary list but excluded from study cards.
- User-contributed DictionaryEntries have `IsPublic = false` — they don't appear
  for other users. Admin validation can promote them to public.

## Quick Add Flow

1. Look up the user's translation text as a DictionaryEntry form.
2. If found → follow BaseEntryId → check EntryTranslation for linked entry in
   the learning language → create UserDictionaryEntry linked to it (Enriched).
3. If not found → create UserDictionaryEntry with `DictionaryEntryId = null`,
   `EnrichmentStatus = Pending`. Worker enriches it later.
4. Phrase vs word determined automatically from input.

## Duplicate Handling

- Dedup uses DictionaryEntry form lookup: user types "книгу" → find entry →
  follow BaseEntryId → "книга" → EntryTranslation → linked base entries.
- If a user already has a UserDictionaryEntry for a given DictionaryEntry,
  the existing entry is updated (merged) rather than creating a duplicate.
- Base vocabulary overlap: if the user adds a word that matches an existing
  public DictionaryEntry, they get a UserDictionaryEntry linked to it without
  triggering enrichment.

## CSV Import

- Required header order: term, translation(s), type.
- Optional columns: Notes, Tags.
- Row-shape errors and malformed input prevent partial import.
- Each row follows the quick add flow.
- Response includes count of items pending enrichment.

## CSV Export

- Returns the user's UserDictionaryEntries joined with DictionaryEntry and
  EntryTranslation data.
- Only enriched items with DictionaryEntryId are exported.

## Clear Custom Data

- Deletes all UserDictionaryEntries, UserEntryContexts, and UserProgress for
  non-public items.
- Preserves DictionaryEntries (shared content layer).
- Preserves UserProgress for public items (base dictionary study progress).
- Does not revoke active sessions.

## Review Checklist

- Does form lookup correctly dedup across morphological variants?
- Does CSV validation remain strict and predictable?
- Does import skip base-overlap and duplicate rows correctly?
- Does export include DictionaryEntry + translation data for enriched items?
- Does clear-custom-data preserve DictionaryEntries and base dictionary progress?
- Does clear-custom-data avoid revoking sessions?
