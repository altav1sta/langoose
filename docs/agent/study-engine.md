# Langoose Study Engine

## Main Files

- `apps/api/src/Langoose.Core/Services/StudyService.cs`
- `apps/api/src/Langoose.Core/Utilities/TextNormalizer.cs`
- `apps/api/tests/Langoose.Core.UnitTests`
- `apps/api/tests/Langoose.Api.IntegrationTests`

## Study Unit

The study unit is an **ExampleSentence with a gap**, not a word. Each sentence is a
complete learning context with its own difficulty, expected answer form, grammar hint,
and full translation.

### Study Card Content

```
ClozeText:           "I need to ____ a hotel for our trip."
SentenceTranslation: "Мне нужно забронировать отель для нашей поездки."
Glosses:             ["забронировать"]  (canonical forms from Gloss table)
GrammarHint:         "infinitive"
Difficulty:          "B1"               (per-sentence, from ExampleSentence)
```

The hint is composite:
- `SentenceTranslation` — full sentence in the target language
- Canonical glosses for the SharedItem in the user's language (from Gloss table)
- `GrammarHint` — what grammatical form the gap expects

## Card Selection

1. Build the studyable pool:
   - All public SharedItems (base dictionary)
   - SharedItems linked from the user's enriched UserItems (SharedItemId is not null)
2. Pick a SharedItem using scheduling (UserProgress order by DueAtUtc, bias toward
   custom items when not overrepresented).
3. Pick an ExampleSentence for that SharedItem (rotate across multiple contexts).
   Also include UserCustomSentences if the user has any.
4. Build the study card from the selected sentence.

### Enrichment Eligibility

Items pending enrichment are excluded from the pool. Only SharedItems with enriched
ExampleSentences participate in study. UserItems with `SharedItemId = null` (pending)
or `EnrichmentStatus = Failed` are not studyable.

## Answer Evaluation

Compare user input against `ExampleSentence.ExpectedAnswer`:
- The sentence determines the correct form — "book", "books", "booked" depending on context.
- Use Levenshtein similarity via TextNormalizer for typo tolerance.
- Missing articles, inflection variants, and minor typos are tolerated as `AlmostCorrect`.
- Record `ExampleSentenceId` in StudyEvent to track which context was tested.

### Verdict Categories

- `Correct` — exact match or near-exact match with the ExpectedAnswer.
- `AlmostCorrect` — minor typo (Levenshtein), missing article, inflection variant.
- `Incorrect` — meaning mismatch or substantially wrong answer.

## UserProgress

Renamed from ReviewState. Tracks spaced repetition per (UserId, SharedItemId):
- `Stability`, `DueAtUtc`, `LapseCount`, `SuccessCount`, `LastSeenAtUtc`
- Created lazily when a SharedItem first appears in study for a user.
- Unique constraint on (UserId, SharedItemId).

## Dashboard

- **Total items**: public SharedItems + user's enriched UserItems.
- **Due now**: count from UserProgress where DueAtUtc <= now.
- **New items**: items in pool with no UserProgress row.
- **Studied today**: StudyEvents in last 24 hours.

## Scheduling Direction

- The current scheduler uses fixed stability increments and fixed intervals.
- M3 plans adoption of FSRS (Free Spaced Repetition Scheduler) to replace it.
- Error analytics should feed back into scheduling: frequently missed words surface
  more often.
- All scheduling algorithm decisions and parameters must be documented when changed.

## Review Checklist

- Did answer evaluation still compare against ExampleSentence.ExpectedAnswer?
- Did Levenshtein tolerance thresholds change?
- Did verdict categories or feedback codes change?
- Did scheduler intervals change?
- Did card balancing between base and custom items change?
- Did dashboard counts still match the visibility rules?
- Did card eligibility still respect enrichment state?
- Is ExampleSentenceId recorded in StudyEvent?
