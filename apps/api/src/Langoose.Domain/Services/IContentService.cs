using Langoose.Domain.Models;

namespace Langoose.Domain.Services;

public interface IContentService
{
    EnrichmentResult Enrich(EnrichmentInput input);

    Task ReportIssueAsync(Guid userId, ReportIssueInput input, CancellationToken cancellationToken);
}
