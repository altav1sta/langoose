namespace Langoose.Domain.Services;

public interface IEnrichmentProcessor
{
    Task ProcessPendingBatchAsync(int batchSize, int maxRetries, CancellationToken cancellationToken);
}
