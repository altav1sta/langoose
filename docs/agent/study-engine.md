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

## Review Checklist

- Did normalization behavior change?
- Did verdict categories or feedback codes change?
- Did scheduler intervals change?
- Did card balancing between base and custom items change?
- Did dashboard counts still match the same visibility rules?
