using Langoose.Domain.Models;

namespace Langoose.Domain.Services;

public interface IContentService
{
    Task ReportIssueAsync(Guid userId, ReportIssueInput input, CancellationToken cancellationToken);
}
