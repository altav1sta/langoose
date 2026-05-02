using Langoose.Domain.Jobs;

namespace Langoose.Domain.Services;

public interface ICorpusImportService
{
    Task<BulkJobState> RunBatchAsync(
        CorpusImportParams settings,
        int batchSize,
        CancellationToken cancellationToken);
}
