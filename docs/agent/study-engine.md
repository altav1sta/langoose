# Langoose Study Engine

## Main Files

- `apps/api/src/Langoose.Api/Services/StudyService.cs`
- `apps/api/src/Langoose.Api/Services/TextNormalizer.cs`
- `apps/api/tests/Langoose.Api.UnitTests`
- `apps/api/tests/Langoose.Api.IntegrationTests`

## Current Behavior

- Due cards come from visible dictionary items, not hidden duplicates.
- Missing review states are created lazily for visible items.
- Card selection orders by due time and success count, then biases toward custom items when custom is not overrepresented.
- Exact matches return `Correct`.
- Accepted variants return `AlmostCorrect` with `AcceptedVariant`.
- Missing articles, inflection variants, and minor typos are intentionally tolerated as `AlmostCorrect`.
- Phrase similarity can also yield `AlmostCorrect`.

## Enrichment Eligibility

- A card is only eligible for study if its dictionary item has enrichment (AI-generated or user-provided context).
- Items in pending enrichment state are excluded from card selection.
- See `docs/agent/enrichment-guidance.md` for enrichment states.

## Scheduling Direction

- The current scheduler uses fixed stability increments and fixed intervals.
- M3 plans adoption of FSRS (Free Spaced Repetition Scheduler) to replace it.
- Error analytics should feed back into scheduling: frequently missed words surface more often.
- All scheduling algorithm decisions and parameters must be documented when changed.

## Review Checklist

- Did normalization behavior change?
- Did verdict categories or feedback codes change?
- Did scheduler intervals change?
- Did card balancing between base and custom items change?
- Did dashboard counts still match the same visibility rules?
- Did card eligibility still respect enrichment state?
