using Langoose.Core.Utilities;
using Langoose.Data;
using Langoose.Domain.Constants;
using Langoose.Domain.Enums;
using Langoose.Domain.Models;
using Langoose.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Core.Services;

public sealed class StudyService(AppDbContext dbContext) : IStudyService
{
    private const double PhraseSimilarityThreshold = 0.60;
    private const double CorrectStabilityCap = 0.95;
    private const double CorrectStabilityIncrement = 0.15;
    private const int CorrectDueIntervalHours = 12;
    private const double AlmostCorrectStabilityCap = 0.85;
    private const double AlmostCorrectStabilityIncrement = 0.08;
    private const int AlmostCorrectDueIntervalHours = 8;
    private const double IncorrectStabilityFloor = 0.20;
    private const double IncorrectStabilityPenalty = 0.12;
    private const int IncorrectDueDelayMinutes = 10;

    public async Task<StudyCard?> GetNextCardAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var dictionaryItems = await dbContext.DictionaryItems.ToListAsync(cancellationToken);
        var reviewStates = await dbContext.ReviewStates
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        var exampleSentences = await dbContext.ExampleSentences.ToListAsync(cancellationToken);

        List<DictionaryItem> visibleItems = [.. GetVisibleItems(dictionaryItems, userId)
            .Where(x => x.Status == DictionaryItemStatus.Active)];
        var reviewStatesByItemId = reviewStates.ToDictionary(x => x.ItemId);

        foreach (var item in visibleItems.Where(x => !reviewStatesByItemId.ContainsKey(x.Id)))
        {
            var reviewState = new ReviewState
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ItemId = item.Id,
                Stability = ReviewDefaults.InitialStability,
                DueAtUtc = DateTimeOffset.UtcNow
            };

            reviewStatesByItemId[item.Id] = reviewState;
            dbContext.ReviewStates.Add(reviewState);
        }

        List<ReviewState> dueStates = [.. reviewStatesByItemId.Values
            .Where(x => visibleItems.Any(item => item.Id == x.ItemId))
            .OrderBy(x => x.DueAtUtc)
            .ThenBy(x => x.SuccessCount)];

        if (dueStates.Count == 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            return null;
        }

        var nextState = PickBalancedItem(dueStates, visibleItems);
        var itemForStudy = visibleItems.First(x => x.Id == nextState.ItemId);
        var sentence = exampleSentences.FirstOrDefault(x => x.ItemId == itemForStudy.Id)
            ?? new ExampleSentence
            {
                Id = Guid.NewGuid(),
                ItemId = itemForStudy.Id,
                SentenceText = itemForStudy.EnglishText,
                ClozeText = "Use ____ in a sentence.",
                TranslationHint = string.Join(", ", itemForStudy.RussianGlosses),
                QualityScore = ExampleQualityScores.PlaceholderFallback,
                Origin = ContentOrigin.Manual
            };

        await dbContext.SaveChangesAsync(cancellationToken);

        return new StudyCard(
            itemForStudy.Id,
            sentence.ClozeText,
            sentence.TranslationHint,
            itemForStudy.RussianGlosses,
            itemForStudy.ItemKind.ToString().ToLowerInvariant(),
            itemForStudy.SourceType.ToString().ToLowerInvariant(),
            itemForStudy.Difficulty);
    }

    public async Task<AnswerResult?> SubmitAnswerAsync(
        Guid userId,
        Guid itemId,
        string submittedAnswer,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.DictionaryItems.FirstOrDefaultAsync(x =>
            x.Id == itemId &&
            (x.OwnerId == null || x.OwnerId == userId),
            cancellationToken);

        if (item is null)
        {
            return null;
        }

        var reviewState = await dbContext.ReviewStates.FirstOrDefaultAsync(x =>
                              x.UserId == userId &&
                              x.ItemId == item.Id,
                              cancellationToken)
                          ;
        var isNewReviewState = reviewState is null;

        reviewState ??= new ReviewState
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ItemId = item.Id,
            Stability = ReviewDefaults.InitialStability,
            DueAtUtc = DateTimeOffset.UtcNow
        };

        if (isNewReviewState)
        {
            dbContext.ReviewStates.Add(reviewState);
        }

        var evaluation = EvaluateAnswer(item, submittedAnswer);
        ApplyScheduler(reviewState, evaluation.Verdict);

        dbContext.StudyEvents.Add(new StudyEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ItemId = item.Id,
            AnsweredAtUtc = DateTimeOffset.UtcNow,
            SubmittedAnswer = submittedAnswer,
            NormalizedAnswer = evaluation.NormalizedAnswer,
            Verdict = evaluation.Verdict,
            FeedbackCode = evaluation.FeedbackCode
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return evaluation with { NextDueAtUtc = reviewState.DueAtUtc };
    }

    public AnswerResult EvaluateAnswer(DictionaryItem item, string submittedAnswer)
    {
        var normalizedSubmitted = TextNormalizer.NormalizeForComparison(submittedAnswer);
        var expected = item.EnglishText;
        var normalizedExpected = TextNormalizer.NormalizeForComparison(expected);
        List<string> acceptedVariants = [.. item.AcceptedVariants
            .Append(expected)
            .Select(TextNormalizer.NormalizeForComparison)
            .Distinct()];

        if (acceptedVariants.Contains(normalizedSubmitted))
        {
            var isVariant = normalizedSubmitted != normalizedExpected;

            return new AnswerResult(
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
            PhraseSimilarity(normalizedSubmitted, normalizedExpected) >= PhraseSimilarityThreshold)
        {
            return CreateAlmostCorrectResult(
                normalizedSubmitted,
                normalizedExpected,
                expected,
                FeedbackCode.AcceptedVariant);
        }

        return new AnswerResult(
            StudyVerdict.Incorrect,
            normalizedSubmitted,
            null,
            expected,
            FeedbackCode.MeaningMismatch,
            DateTimeOffset.UtcNow);
    }

    public async Task<ProgressDashboard> GetDashboardAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var dictionaryItems = await dbContext.DictionaryItems.ToListAsync(cancellationToken);
        var items = GetVisibleItems(dictionaryItems, userId);
        var visibleItemIds = items.Select(x => x.Id).ToHashSet();
        var states = await dbContext.ReviewStates
            .Where(x => x.UserId == userId && visibleItemIds.Contains(x.ItemId))
            .ToListAsync(cancellationToken);
        var startOfTodayUtc = DateTimeOffset.UtcNow.UtcDateTime.Date;
        var endOfTodayUtc = startOfTodayUtc.AddDays(1);
        var studiedToday = await dbContext.StudyEvents.CountAsync(x =>
            x.UserId == userId &&
            x.AnsweredAtUtc >= startOfTodayUtc &&
            x.AnsweredAtUtc < endOfTodayUtc &&
            visibleItemIds.Contains(x.ItemId), cancellationToken);

        return new ProgressDashboard(
            items.Count,
            states.Count(x => x.DueAtUtc <= DateTimeOffset.UtcNow),
            states.Count(x => x.SuccessCount == 0),
            items.Count(x => x.SourceType == SourceType.Base),
            items.Count(x => x.SourceType == SourceType.Custom),
            studiedToday);
    }

    private static List<DictionaryItem> GetVisibleItems(List<DictionaryItem> dictionaryItems, Guid userId)
    {
        return [.. dictionaryItems
            .Where(x => x.OwnerId is null || x.OwnerId == userId)
            .GroupBy(x => TextNormalizer.NormalizeForComparison(x.EnglishText))
            .Select(group => group
                .OrderByDescending(x => x.OwnerId == userId)
                .ThenBy(x => x.CreatedAtUtc)
                .First())];
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
                reviewState.Stability = Math.Min(CorrectStabilityCap, reviewState.Stability + CorrectStabilityIncrement);
                reviewState.DueAtUtc = DateTimeOffset.UtcNow.AddHours(
                    CorrectDueIntervalHours * Math.Max(1, reviewState.SuccessCount));
                break;
            case StudyVerdict.AlmostCorrect:
                reviewState.SuccessCount += 1;
                reviewState.Stability = Math.Min(
                    AlmostCorrectStabilityCap,
                    reviewState.Stability + AlmostCorrectStabilityIncrement);
                reviewState.DueAtUtc = DateTimeOffset.UtcNow.AddHours(
                    AlmostCorrectDueIntervalHours * Math.Max(1, reviewState.SuccessCount));
                break;
            default:
                reviewState.LapseCount += 1;
                reviewState.Stability = Math.Max(IncorrectStabilityFloor, reviewState.Stability - IncorrectStabilityPenalty);
                reviewState.DueAtUtc = DateTimeOffset.UtcNow.AddMinutes(IncorrectDueDelayMinutes);
                break;
        }
    }

    private static AnswerResult CreateAlmostCorrectResult(
        string normalizedSubmitted,
        string normalizedExpected,
        string expected,
        FeedbackCode feedbackCode)
    {
        return new AnswerResult(
            StudyVerdict.AlmostCorrect,
            normalizedSubmitted,
            normalizedExpected,
            expected,
            feedbackCode,
            DateTimeOffset.UtcNow);
    }
}
