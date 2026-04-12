using Langoose.Domain.Models;

namespace Langoose.Domain.Services;

public interface IStudyService
{
    Task<StudyCard?> GetNextCardAsync(Guid userId, CancellationToken cancellationToken);

    Task<AnswerResult?> SubmitAnswerAsync(Guid userId, Guid entryId, Guid? entryContextId, string submittedAnswer, CancellationToken cancellationToken);

    AnswerResult EvaluateAnswer(DictionaryEntry entry, string submittedAnswer);

    Task<ProgressDashboard> GetDashboardAsync(Guid userId, CancellationToken cancellationToken);
}
