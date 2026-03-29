# Langoose Study Rules

## Main Files

- `apps/api/Services/StudyService.cs`
- `apps/api/Services/TextNormalizer.cs`
- `tests/Langoose.Api.Tests`

## Current Behavior

- Due cards come from visible dictionary items, not hidden duplicates.
- Missing review states are created lazily for visible items.
- Card selection orders by due time and success count, then biases toward custom items when custom is not already overrepresented.
- Exact matches return `Correct`.
- Accepted variants return `AlmostCorrect` with `AcceptedVariant`.
- Missing articles, inflection variants, and minor typos are intentionally tolerated as `AlmostCorrect`.
- Phrase similarity can also yield an `AlmostCorrect` result.
- Incorrect answers push the next due time close, while correct and almost-correct answers space further out.

## Review Checklist

- Did normalization behavior change?
- Did verdict categories or feedback codes change?
- Did scheduler intervals change?
- Did card balancing between base and custom items change?
- Did dashboard counts still match the same underlying visibility rules?

## Recommendations

- If study logic grows, split `StudyService` responsibilities into clearly named collaborators without weakening the current tests.
- If normalization rules grow, keep them explicit and testable rather than burying them in ad hoc string comparisons.
