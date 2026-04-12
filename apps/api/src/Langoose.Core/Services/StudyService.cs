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
        var studyableEntryIds = await GetStudyableEntryIdsAsync(userId, cancellationToken);

        if (studyableEntryIds.Count == 0)
        {
            return null;
        }

        var existingProgress = await dbContext.UserProgress
            .Where(x => x.UserId == userId && studyableEntryIds.Contains(x.DictionaryEntryId))
            .ToDictionaryAsync(x => x.DictionaryEntryId, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        foreach (var entryId in studyableEntryIds.Where(id => !existingProgress.ContainsKey(id)))
        {
            var progress = new UserProgress
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                DictionaryEntryId = entryId,
                DueAtUtc = now,
                Stability = ProgressDefaults.InitialStability,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.UserProgress.Add(progress);
            existingProgress[entryId] = progress;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var next = existingProgress.Values
            .OrderBy(x => x.DueAtUtc)
            .ThenBy(x => x.SuccessCount)
            .First();

        var entry = await dbContext.DictionaryEntries
            .Include(x => x.Translations)
            .FirstAsync(x => x.Id == next.DictionaryEntryId, cancellationToken);

        var context = await dbContext.EntryContexts
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(x => x.DictionaryEntryId == next.DictionaryEntryId, cancellationToken);

        var translations = entry.Translations
            .Select(x => x.Text)
            .ToList();

        var sentenceTranslation = context?.Translations
            .Select(x => x.Text)
            .FirstOrDefault() ?? "";

        return new StudyCard(
            next.DictionaryEntryId,
            context?.Id,
            context?.Cloze ?? "Use ____ in a sentence.",
            sentenceTranslation,
            translations,
            BuildGrammarHint(entry),
            context?.Difficulty ?? entry.Difficulty);
    }

    private async Task<List<Guid>> GetStudyableEntryIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var publicEntryIds = await dbContext.DictionaryEntries
            .Where(x => x.IsPublic && x.BaseEntryId == null)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var enrichedEntryIds = await dbContext.UserDictionaryEntries
            .Where(x => x.UserId == userId && x.DictionaryEntryId != null)
            .Select(x => x.DictionaryEntryId!.Value)
            .ToListAsync(cancellationToken);

        var flaggedEntryIds = await dbContext.ContentFlags
            .Where(x => x.ReportedByUserId == userId && !x.IsResolved)
            .Select(x => x.DictionaryEntryId)
            .ToListAsync(cancellationToken);
        var flaggedSet = flaggedEntryIds.ToHashSet();

        return publicEntryIds.Union(enrichedEntryIds)
            .Where(id => !flaggedSet.Contains(id))
            .ToList();
    }

    public async Task<AnswerResult?> SubmitAnswerAsync(
        Guid userId,
        Guid entryId,
        Guid? entryContextId,
        string submittedAnswer,
        CancellationToken cancellationToken)
    {
        var entry = await dbContext.DictionaryEntries
            .FirstOrDefaultAsync(x => x.Id == entryId, cancellationToken);

        if (entry is null)
        {
            return null;
        }

        var progress = await dbContext.UserProgress
            .FirstOrDefaultAsync(x => x.UserId == userId && x.DictionaryEntryId == entryId, cancellationToken);
        var isNew = progress is null;

        progress ??= new UserProgress
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            DictionaryEntryId = entryId,
            DueAtUtc = DateTimeOffset.UtcNow,
            Stability = ProgressDefaults.InitialStability,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        if (isNew)
        {
            dbContext.UserProgress.Add(progress);
        }

        var evaluation = EvaluateAnswer(entry, submittedAnswer);
        ApplyScheduler(progress, evaluation.Verdict);

        var validatedContextId = entryContextId.HasValue
            ? await dbContext.EntryContexts
                .Where(x => x.Id == entryContextId.Value && x.DictionaryEntryId == entryId)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        dbContext.StudyEvents.Add(new StudyEvent
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            DictionaryEntryId = entryId,
            EntryContextId = validatedContextId,
            UserInput = submittedAnswer,
            Verdict = evaluation.Verdict,
            FeedbackCode = evaluation.FeedbackCode,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return evaluation with { NextDueAtUtc = progress.DueAtUtc };
    }

    public AnswerResult EvaluateAnswer(DictionaryEntry entry, string submittedAnswer)
    {
        var normalizedSubmitted = TextNormalizer.NormalizeForComparison(submittedAnswer);
        var expected = entry.Text;
        var normalizedExpected = TextNormalizer.NormalizeForComparison(expected);

        if (normalizedSubmitted == normalizedExpected)
        {
            return new AnswerResult(
                StudyVerdict.Correct,
                normalizedSubmitted,
                expected,
                FeedbackCode.ExactMatch,
                DateTimeOffset.UtcNow);
        }

        if (TextNormalizer.TokensMatchIgnoringArticles(submittedAnswer, expected))
        {
            return CreateAlmostCorrectResult(normalizedSubmitted, expected, FeedbackCode.MissingArticle);
        }

        if (TextNormalizer.LooksLikeInflectionVariant(submittedAnswer, expected))
        {
            return CreateAlmostCorrectResult(normalizedSubmitted, expected, FeedbackCode.InflectionMismatch);
        }

        if (TextNormalizer.LooksLikeMinorTypo(submittedAnswer, expected))
        {
            return CreateAlmostCorrectResult(normalizedSubmitted, expected, FeedbackCode.MinorTypo);
        }

        return new AnswerResult(
            StudyVerdict.Incorrect,
            normalizedSubmitted,
            expected,
            FeedbackCode.MeaningMismatch,
            DateTimeOffset.UtcNow);
    }

    public async Task<ProgressDashboard> GetDashboardAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var studyableEntryIds = await GetStudyableEntryIdsAsync(userId, cancellationToken);
        var studyableIdSet = studyableEntryIds.ToHashSet();

        var progressItems = await dbContext.UserProgress
            .Where(x => x.UserId == userId && studyableIdSet.Contains(x.DictionaryEntryId))
            .ToListAsync(cancellationToken);
        var progressEntryIds = progressItems.Select(x => x.DictionaryEntryId).ToHashSet();

        var newEntries = studyableEntryIds.Count(id => !progressEntryIds.Contains(id));

        var startOfTodayUtc = DateTimeOffset.UtcNow.UtcDateTime.Date;
        var endOfTodayUtc = startOfTodayUtc.AddDays(1);
        var studiedToday = await dbContext.StudyEvents.CountAsync(x =>
            x.UserId == userId &&
            x.CreatedAtUtc >= startOfTodayUtc &&
            x.CreatedAtUtc < endOfTodayUtc, cancellationToken);

        return new ProgressDashboard(
            studyableEntryIds.Count,
            progressItems.Count(x => x.DueAtUtc <= DateTimeOffset.UtcNow) + newEntries,
            newEntries,
            studiedToday);
    }

    private static void ApplyScheduler(UserProgress progress, StudyVerdict verdict)
    {
        progress.LastReviewedAtUtc = DateTimeOffset.UtcNow;
        progress.UpdatedAtUtc = DateTimeOffset.UtcNow;

        switch (verdict)
        {
            case StudyVerdict.Correct:
                progress.SuccessCount += 1;
                progress.Stability = Math.Min(CorrectStabilityCap, (progress.Stability ?? 0) + CorrectStabilityIncrement);
                progress.DueAtUtc = DateTimeOffset.UtcNow.AddHours(
                    CorrectDueIntervalHours * Math.Max(1, progress.SuccessCount));
                break;
            case StudyVerdict.AlmostCorrect:
                progress.SuccessCount += 1;
                progress.Stability = Math.Min(
                    AlmostCorrectStabilityCap,
                    (progress.Stability ?? 0) + AlmostCorrectStabilityIncrement);
                progress.DueAtUtc = DateTimeOffset.UtcNow.AddHours(
                    AlmostCorrectDueIntervalHours * Math.Max(1, progress.SuccessCount));
                break;
            default:
                progress.FailureCount += 1;
                progress.Stability = Math.Max(IncorrectStabilityFloor, (progress.Stability ?? 0) - IncorrectStabilityPenalty);
                progress.DueAtUtc = DateTimeOffset.UtcNow.AddMinutes(IncorrectDueDelayMinutes);
                break;
        }
    }

    private static string? BuildGrammarHint(DictionaryEntry entry)
    {
        var hint = string.Join(", ", new[] { entry.PartOfSpeech, entry.GrammarLabel }.Where(x => x is not null));
        return hint.Length > 0 ? hint : null;
    }

    private static AnswerResult CreateAlmostCorrectResult(
        string normalizedSubmitted,
        string expected,
        FeedbackCode feedbackCode)
    {
        return new AnswerResult(
            StudyVerdict.AlmostCorrect,
            normalizedSubmitted,
            expected,
            feedbackCode,
            DateTimeOffset.UtcNow);
    }
}
