# Langoose Study Engine

## Main Files

- `apps/api/src/Langoose.Core/Services/StudyService.cs`
- `apps/api/src/Langoose.Core/Utilities/TextNormalizer.cs`
- `apps/api/tests/Langoose.Core.UnitTests`
- `apps/api/tests/Langoose.Api.IntegrationTests`

## Study Unit

The study unit is an **EntryContext** — a sentence with a cloze gap linked to a
specific DictionaryEntry form. The expected answer and grammar hint are derived
from the linked entry, not stored on the context.

### Study Card Content

```
Cloze:              "She ____ the room yesterday."
Sentence hint:      "Она забронировала комнату вчера."  (paired context via Translations)
Translations:       ["забронировать"]                   (via Translations navigation)
Grammar hint:       "past simple"                       (from DictionaryEntry.GrammarLabel)
Expected answer:    "booked"                            (from DictionaryEntry.Text)
Difficulty:         "B1"                                (from EntryContext.Difficulty)
```

## Card Selection

1. Build the studyable pool:
   - All public DictionaryEntries (base forms)
   - DictionaryEntries linked from the user's enriched UserDictionaryEntries
2. Exclude entries with no EntryContexts, pending/failed UserDictionaryEntries.
3. Join UserProgress for scheduling (lazy create for new encounters).
4. Order by DueAtUtc, then SuccessCount. Bias toward custom items.
5. Pick a base entry → pick an EntryContext for one of its forms (rotate contexts).
6. Include UserEntryContexts if the user has added their own.

## Answer Evaluation

Grading is intentionally tolerant. Compare user input against the linked
DictionaryEntry.Text (the expected form):
- Levenshtein similarity via TextNormalizer for typo tolerance
- Missing articles, inflection variants tolerated as AlmostCorrect
- Accepted known variants (e.g. "colour" vs "color") as Correct
- Record EntryContextId in StudyEvent for per-context analytics

### Verdicts

- `Correct` — exact match, near-exact, or accepted variant
- `AlmostCorrect` — minor typo, missing article, inflection variant
- `Incorrect` — meaning mismatch

## UserProgress

Tracks spaced repetition per (UserId, DictionaryEntryId). Created lazily.
Unique constraint on (UserId, DictionaryEntryId).

## Dashboard

- Total items: public base entries + user's enriched entries
- Due now: UserProgress where DueAtUtc <= now
- New items: entries in pool with no UserProgress row
- Studied today: StudyEvents in last 24h

## Scheduling Direction

- Current scheduler uses fixed stability increments and intervals.
- M3 plans FSRS adoption.
- Error analytics should feed back into scheduling.
- All scheduling parameter changes must be documented.

## Review Checklist

- Did answer evaluation still compare against DictionaryEntry.Text?
- Did Levenshtein tolerance thresholds change?
- Did verdict categories or feedback codes change?
- Did scheduler intervals change?
- Did card balancing between base and custom items change?
- Did dashboard counts still match the visibility rules?
- Is EntryContextId recorded in StudyEvent?
