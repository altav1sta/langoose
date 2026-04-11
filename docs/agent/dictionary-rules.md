# Langoose Dictionary Rules

## Main Files

- `apps/api/src/Langoose.Core/Services/DictionaryService.cs`
- `apps/api/src/Langoose.Api/Controllers/DictionaryController.cs`
- `apps/api/tests/Langoose.Core.UnitTests`
- `apps/api/tests/Langoose.Api.IntegrationTests`
- `apps/web/src/api.ts`

## Data Model

The dictionary uses a two-layer model:

- **SharedItem** — enriched English word/phrase concepts. Contains `NormalizedText`,
  `PartOfSpeech`, `Difficulty`, `IsPublic`, `Source`. Only validated content lives here.
- **UserItem** — per-user dictionary entries referencing a SharedItem. Contains
  `RawEnglishText`, `RawGloss`, `Language`, `EnrichmentStatus`, notes, tags, status.
  `SharedItemId` is nullable (null while pending enrichment).
- **Gloss** — canonical translations linking SharedItem to a language.
- **GlossSurfaceForm** — morphological variants pointing to a Gloss for dedup lookups.

## Visibility Rules

- Users see all public SharedItems (base dictionary) plus their own UserItems that
  have been enriched (SharedItemId is not null).
- Pending UserItems (SharedItemId is null) are visible in the dictionary list but
  excluded from study cards.
- User-contributed SharedItems have `IsPublic = false` — they don't appear for other
  users. Admin validation can promote them to public.

## Quick Add Flow

1. Normalize the English text.
2. Look up GlossSurfaceForm for the user's gloss in the target language.
3. If a match is found → Gloss → SharedItem → create UserItem linked to it (Enriched).
4. If no match → create UserItem with `SharedItemId = null`, `EnrichmentStatus = Pending`.
   The background worker enriches it later.
5. Phrase vs word determined automatically from input (contains spaces → phrase).

## Duplicate Handling

- Dedup key is GlossSurfaceForm → Gloss → SharedItem. Two users adding the same word
  with different morphological forms of the same gloss land on the same SharedItem.
- If a user already has a UserItem for a given SharedItem, the existing UserItem is
  updated (merged) rather than creating a duplicate.
- Base vocabulary overlap: if the user adds a word that matches an existing public
  SharedItem, they get a UserItem linked to it without triggering enrichment.

## CSV Import

- Required header order: English term, Russian translation(s), Type.
- Optional columns: Notes, Tags.
- Row-shape errors and malformed input prevent partial import — no half-imported files.
- Each row follows the quick add flow (GlossSurfaceForm lookup → link or create Pending).
- Response includes count of items pending enrichment.

## CSV Export

- Returns the user's UserItems joined with SharedItem and Gloss data.
- Only enriched items with SharedItemId are exported.

## Clear Custom Data

- Deletes all UserItems, UserCustomSentences, and UserProgress for non-public items.
- Preserves SharedItems (shared content layer).
- Preserves UserProgress for public items (base dictionary study progress).
- Does not revoke active sessions.

## Review Checklist

- Does GlossSurfaceForm lookup correctly dedup across morphological variants?
- Does CSV validation remain strict and predictable?
- Does import skip base-overlap and duplicate rows correctly?
- Does export include SharedItem + Gloss data for enriched items?
- Does clear-custom-data preserve SharedItems and base dictionary progress?
- Does clear-custom-data avoid revoking sessions?
