using Langoose.Domain.Jobs;

namespace Langoose.Domain.Services;

public interface IUserEntriesImportService
{
    Task<BulkJobState> RunBatchAsync(
        int batchSize, int maxRetries, CancellationToken cancellationToken);
}
