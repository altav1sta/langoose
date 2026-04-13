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
  `PartOfSpeech` is required. `IsPublic` controls visibility.
- **Translations** (implicit M2M) — links base forms across languages
  (source → target). Join table: `dictionary_entries_translations`.
- **UserDictionaryEntry** — per-user custom entries with `SourceEntryId` and
  `TargetEntryId` FKs (both nullable, set by the enrichment worker).

## Visibility Rules

- Users see all public base DictionaryEntries plus their own UserDictionaryEntries.
- Pending UserDictionaryEntries (SourceEntryId is null) are visible in the
  dictionary list but excluded from study cards.
- Enrichment-created DictionaryEntries have `IsPublic = false` — they don't appear
  for other users. Admin validation can promote them to public.

## Quick Add Flow

1. User provides term, optional translation, source/target languages, and POS.
2. Check if user already has a matching entry (same user, sourceLang, targetLang,
   POS, term, translation) — if so, return existing.
3. Otherwise create UserDictionaryEntry with `EnrichmentStatus = Pending`.
4. Worker handles lookup, validation, entry creation, and linking.

## Duplicate Handling

- Dedup checks `(userId, sourceLang, POS, term)` in DB, then filters by
  `targetLanguage` and `translation` in memory.
- Same term with different translation creates a new entry (different meaning).
- Same term with different POS creates a new entry (different word class).
- Exact duplicates return the existing entry without modification.

## CSV Import

- Required header order: English term, Russian translation(s), Part of Speech.
  Also accepts legacy "Type" header.
- Optional columns: Notes, Tags.
- Row-shape errors and malformed input prevent partial import.
- Dedup uses `(term, translation, POS)` — skips rows matching existing entries.
- Response includes count of items pending enrichment.

## CSV Export

- Returns the user's UserDictionaryEntries joined with SourceEntry and
  translation data (via Translations navigation).
- Header: English term, Russian translation(s), Part of Speech, Notes, Tags.

## Clear Custom Data

- Deletes all UserDictionaryEntries, UserProgress, StudyEvents for non-public
  items, ImportRecords, and ContentFlags for the user.
- Preserves DictionaryEntries (shared content layer).
- Preserves UserProgress for public items (base dictionary study progress).
- Does not revoke active sessions.

## Review Checklist

- Does dedup correctly match on (term, translation, POS, languages)?
- Does CSV validation remain strict and predictable?
- Does import skip duplicate rows correctly?
- Does export include SourceEntry + translation data?
- Does clear-custom-data preserve DictionaryEntries and base dictionary progress?
- Does clear-custom-data avoid revoking sessions?
