using Langoose.Domain.Models;

namespace Langoose.Domain.Services;

public interface IEnrichmentProvider
{
    Task<EnrichmentResult[]> EnrichBatchAsync(
        UserEntry[] batch, CancellationToken cancellationToken);
}
