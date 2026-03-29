# Langoose Dictionary Rules

## Main Files

- `apps/api/Langoose.Api/Services/DictionaryService.cs`
- `apps/api/Langoose.Api/Controllers/DictionaryController.cs`
- `tests/Langoose.Api.Tests`
- `apps/web/src/api.ts`
- `apps/web/src/App.tsx`

## Current Behavior

- Visible items are grouped by normalized English text and prefer the user-owned version when present.
- Duplicate custom entries are merged during reads and writes.
- Base vocabulary overlap does not create a second visible item.
- Quick add determines phrase vs word automatically when item kind is missing.
- Import requires header order starting with `English term`, `Russian translation(s)`, `Type`.
- Only `Notes` and `Tags` are allowed as optional CSV columns.
- Row-shape errors and malformed input prevent partial import.
- Export returns only the user's visible custom items in CSV format.
- Clearing custom data removes custom items, example sentences, review states, study events, imports, and content flags for the user, but keeps session tokens.

## Review Checklist

- Did duplicate normalization still collapse visible entries correctly?
- Did CSV validation remain strict and predictable?
- Did import skip base-overlap and duplicate rows correctly?
- Did export still match the intended visible custom subset?
- Did clear-custom-data avoid revoking sessions?

## Recommendations

- If CSV handling grows more complex, isolate parsing and validation into dedicated collaborators while keeping behavior tests intact.
- Split `ApiModels.cs` and `Entities.cs` into one type per file during future cleanup to match the repo conventions.
