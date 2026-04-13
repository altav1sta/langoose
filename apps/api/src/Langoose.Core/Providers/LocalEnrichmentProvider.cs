using Langoose.Domain.Enums;
using Langoose.Domain.Models;
using Langoose.Domain.Services;

namespace Langoose.Core.Providers;

public sealed class LocalEnrichmentProvider : IEnrichmentProvider
{
    public Task<EnrichmentResult[]> EnrichBatchAsync(
        UserDictionaryEntry[] batch, CancellationToken cancellationToken)
    {
        var results = new EnrichmentResult[batch.Length];

        for (var i = 0; i < batch.Length; i++)
            results[i] = Enrich(batch[i]);

        return Task.FromResult(results);
    }

    private static EnrichmentResult Enrich(UserDictionaryEntry item)
    {
        EnrichedEntry[]? sourceEntries = item.SourceEntry == null
            ? [new EnrichedEntry(item.UserInputTerm.Trim(), IsBaseForm: true, null, null)]
            : null;

        var targetText = item.UserInputTranslation?.Trim() ?? item.UserInputTerm.Trim();

        EnrichedEntry[]? targetEntries = item.TargetEntry == null
            ? [new EnrichedEntry(targetText, IsBaseForm: true, null, null)]
            : null;

        return new EnrichmentResult(item.Id, EnrichmentStatus.Enriched, sourceEntries, targetEntries);
    }
}
