using Langoose.Api.Infrastructure;
using Langoose.Api.Models;

namespace Langoose.Api.Services;

public sealed class StudyService(IDataStore dataStore)
{
    public async Task<StudyCardResponse?> GetNextCardAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var store = await dataStore.LoadAsync(cancellationToken);
        var visibleItems = GetVisibleItems(store, userId)
            .Where(item => item.Status == DictionaryItemStatus.Active)
            .ToList();

        var reviewStates = store.ReviewStates
            .Where(state => state.UserId == userId)
            .ToDictionary(state => state.ItemId);

        foreach (var item in visibleItems.Where(item => !reviewStates.ContainsKey(item.Id)))
        {
            var reviewState = new ReviewState
            {
                UserId = userId,
                ItemId = item.Id,
                DueAtUtc = DateTimeOffset.UtcNow
            };

            reviewStates[item.Id] = reviewState;
            store.ReviewStates.Add(reviewState);
        }

        var dueStates = reviewStates.Values
            .Where(state => visibleItems.Any(item => item.Id == state.ItemId))
            .OrderBy(state => state.DueAtUtc)
            .ThenBy(state => state.SuccessCount)
            .ToList();

        if (dueStates.Count == 0)
        {
            await dataStore.SaveAsync(store, cancellationToken);

            return null;
        }

        var nextState = PickBalancedItem(dueStates, visibleItems);
        var itemForStudy = visibleItems.First(item => item.Id == nextState.ItemId);
        var sentence = store.ExampleSentences.FirstOrDefault(candidate => candidate.ItemId == itemForStudy.Id)
            ?? new ExampleSentence
            {
                ItemId = itemForStudy.Id,
                SentenceText = itemForStudy.EnglishText,
                ClozeText = "Use ____ in a sentence.",
                TranslationHint = string.Join(", ", itemForStudy.RussianGlosses),
                Origin = ContentOrigin.Manual
            };

        await dataStore.SaveAsync(store, cancellationToken);

        return new StudyCardResponse(
            itemForStudy.Id,
            sentence.ClozeText,
            sentence.TranslationHint,
            itemForStudy.RussianGlosses,
            itemForStudy.ItemKind.ToString().ToLowerInvariant(),
            itemForStudy.SourceType.ToString().ToLowerInvariant(),
            itemForStudy.Difficulty);
    }

    public async Task<StudyAnswerResult?> SubmitAnswerAsync(
        Guid userId,
        StudyAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var store = await dataStore.LoadAsync(cancellationToken);
        var item = store.DictionaryItems.FirstOrDefault(candidate =>
            candidate.Id == request.ItemId &&
            (candidate.OwnerId is null || candidate.OwnerId == userId));

        if (item is null)
        {
            return null;
        }

        var reviewState = store.ReviewStates.FirstOrDefault(state =>
                              state.UserId == userId &&
                              state.ItemId == item.Id)
                          ?? new ReviewState
                          {
                              UserId = userId,
                              ItemId = item.Id,
                              DueAtUtc = DateTimeOffset.UtcNow
                          };

        if (!store.ReviewStates.Any(state => state.Id == reviewState.Id))
        {
            store.ReviewStates.Add(reviewState);
        }

        var evaluation = EvaluateAnswer(item, request.SubmittedAnswer);
        ApplyScheduler(reviewState, evaluation.Verdict);

        store.StudyEvents.Add(new StudyEvent
        {
            UserId = userId,
            ItemId = item.Id,
            SubmittedAnswer = request.SubmittedAnswer,
            NormalizedAnswer = evaluation.NormalizedAnswer,
            Verdict = evaluation.Verdict,
            FeedbackCode = evaluation.FeedbackCode
        });

        await dataStore.SaveAsync(store, cancellationToken);

        return evaluation with { NextDueAtUtc = reviewState.DueAtUtc };
    }

    public StudyAnswerResult EvaluateAnswer(DictionaryItem item, string submittedAnswer)
    {
        var normalizedSubmitted = TextNormalizer.NormalizeForComparison(submittedAnswer);
        var expected = item.EnglishText;
        var normalizedExpected = TextNormalizer.NormalizeForComparison(expected);
        var acceptedVariants = item.AcceptedVariants
            .Append(expected)
            .Select(TextNormalizer.NormalizeForComparison)
            .Distinct()
            .ToList();

        if (acceptedVariants.Contains(normalizedSubmitted))
        {
            var isVariant = normalizedSubmitted != normalizedExpected;

            return new StudyAnswerResult(
                isVariant ? StudyVerdict.AlmostCorrect : StudyVerdict.Correct,
                normalizedSubmitted,
                normalizedSubmitted,
                expected,
                isVariant ? FeedbackCode.AcceptedVariant : FeedbackCode.ExactMatch,
                DateTimeOffset.UtcNow);
        }

        if (TextNormalizer.TokensMatchIgnoringArticles(submittedAnswer, expected))
        {
            return CreateAlmostCorrectResult(
                normalizedSubmitted,
                normalizedExpected,
                expected,
                FeedbackCode.MissingArticle);
        }

        if (TextNormalizer.LooksLikeInflectionVariant(submittedAnswer, expected))
        {
            return CreateAlmostCorrectResult(
                normalizedSubmitted,
                normalizedExpected,
                expected,
                FeedbackCode.InflectionMismatch);
        }

        if (TextNormalizer.LooksLikeMinorTypo(submittedAnswer, expected))
        {
            return CreateAlmostCorrectResult(
                normalizedSubmitted,
                normalizedExpected,
                expected,
                FeedbackCode.MinorTypo);
        }

        if (item.ItemKind == ItemKind.Phrase &&
            PhraseSimilarity(normalizedSubmitted, normalizedExpected) >= 0.6)
        {
            return CreateAlmostCorrectResult(
                normalizedSubmitted,
                normalizedExpected,
                expected,
                FeedbackCode.AcceptedVariant);
        }

        return new StudyAnswerResult(
            StudyVerdict.Incorrect,
            normalizedSubmitted,
            null,
            expected,
            FeedbackCode.MeaningMismatch,
            DateTimeOffset.UtcNow);
    }

    public async Task<ProgressDashboardResponse> GetDashboardAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var store = await dataStore.LoadAsync(cancellationToken);
        var items = GetVisibleItems(store, userId);
        var visibleItemIds = items.Select(item => item.Id).ToHashSet();
        var states = store.ReviewStates
            .Where(state => state.UserId == userId && visibleItemIds.Contains(state.ItemId))
            .ToList();
        var today = DateTimeOffset.UtcNow.Date;
        var studiedToday = store.StudyEvents.Count(ev =>
            ev.UserId == userId &&
            ev.AnsweredAtUtc.UtcDateTime.Date == today &&
            visibleItemIds.Contains(ev.ItemId));

        return new ProgressDashboardResponse(
            items.Count,
            states.Count(state => state.DueAtUtc <= DateTimeOffset.UtcNow),
            states.Count(state => state.SuccessCount == 0),
            items.Count(item => item.SourceType == SourceType.Base),
            items.Count(item => item.SourceType == SourceType.Custom),
            studiedToday);
    }

    private static List<DictionaryItem> GetVisibleItems(DataStore store, Guid userId)
    {
        return store.DictionaryItems
            .Where(item => item.OwnerId is null || item.OwnerId == userId)
            .GroupBy(item => TextNormalizer.NormalizeForComparison(item.EnglishText))
            .Select(group => group
                .OrderByDescending(item => item.OwnerId == userId)
                .ThenBy(item => item.CreatedAtUtc)
                .First())
            .ToList();
    }

    private static double PhraseSimilarity(string left, string right)
    {
        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rightTokens = right.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (leftTokens.Length == 0 || rightTokens.Length == 0)
        {
            return 0;
        }

        var matches = leftTokens.Intersect(rightTokens).Count();

        return (double)matches / Math.Max(leftTokens.Length, rightTokens.Length);
    }

    private static ReviewState PickBalancedItem(
        List<ReviewState> dueStates,
        List<DictionaryItem> visibleItems)
    {
        var sourceCounts = dueStates
            .Join(visibleItems, state => state.ItemId, item => item.Id, (_, item) => item.SourceType)
            .GroupBy(sourceType => sourceType)
            .ToDictionary(group => group.Key, group => group.Count());

        var customCandidate = dueStates.FirstOrDefault(state =>
            visibleItems.Any(item =>
                item.Id == state.ItemId &&
                item.SourceType == SourceType.Custom) &&
            sourceCounts.GetValueOrDefault(SourceType.Custom) <=
            sourceCounts.GetValueOrDefault(SourceType.Base));

        return customCandidate ?? dueStates[0];
    }

    private static void ApplyScheduler(ReviewState reviewState, StudyVerdict verdict)
    {
        reviewState.LastSeenAtUtc = DateTimeOffset.UtcNow;

        switch (verdict)
        {
            case StudyVerdict.Correct:
                reviewState.SuccessCount += 1;
                reviewState.Stability = Math.Min(0.95, reviewState.Stability + 0.15);
                reviewState.DueAtUtc = DateTimeOffset.UtcNow.AddHours(
                    12 * Math.Max(1, reviewState.SuccessCount));
                break;
            case StudyVerdict.AlmostCorrect:
                reviewState.SuccessCount += 1;
                reviewState.Stability = Math.Min(0.85, reviewState.Stability + 0.08);
                reviewState.DueAtUtc = DateTimeOffset.UtcNow.AddHours(
                    8 * Math.Max(1, reviewState.SuccessCount));
                break;
            default:
                reviewState.LapseCount += 1;
                reviewState.Stability = Math.Max(0.2, reviewState.Stability - 0.12);
                reviewState.DueAtUtc = DateTimeOffset.UtcNow.AddMinutes(10);
                break;
        }
    }

    private static StudyAnswerResult CreateAlmostCorrectResult(
        string normalizedSubmitted,
        string normalizedExpected,
        string expected,
        FeedbackCode feedbackCode)
    {
        return new StudyAnswerResult(
            StudyVerdict.AlmostCorrect,
            normalizedSubmitted,
            normalizedExpected,
            expected,
            feedbackCode,
            DateTimeOffset.UtcNow);
    }
}
