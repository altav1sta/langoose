using Langoose.Domain.Models;

namespace Langoose.Domain.Services;

public interface IStudyService
{
    Task<StudyCard?> GetNextCardAsync(Guid userId, CancellationToken cancellationToken);

    Task<AnswerResult?> SubmitAnswerAsync(Guid userId, Guid itemId, string submittedAnswer, CancellationToken cancellationToken);

    AnswerResult EvaluateAnswer(DictionaryItem item, string submittedAnswer);

    Task<ProgressDashboard> GetDashboardAsync(Guid userId, CancellationToken cancellationToken);
}
