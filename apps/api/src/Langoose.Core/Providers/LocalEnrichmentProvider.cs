using Langoose.Domain.Models;
using Langoose.Domain.Services;

namespace Langoose.Core.Providers;

public sealed class LocalEnrichmentProvider : IEnrichmentProvider
{
    public Task<EnrichmentResult[]> EnrichBatchAsync(
        EnrichmentRequest[] batch, CancellationToken cancellationToken)
    {
        var results = new EnrichmentResult[batch.Length];

        for (var i = 0; i < batch.Length; i++)
        {
            results[i] = Enrich(batch[i]);
        }

        return Task.FromResult(results);
    }

    private static EnrichmentResult Enrich(EnrichmentRequest request)
    {
        var sourceEntry = new EnrichedEntry(
            Text: request.RawText.Trim(),
            IsBaseForm: true,
            BaseFormText: null,
            GrammarLabel: null,
            Difficulty: null);

        if (string.IsNullOrWhiteSpace(request.RawTranslation))
        {
            return new EnrichmentResult([sourceEntry], [], []);
        }

        var translation = request.RawTranslation.Trim();

        var targetEntry = new EnrichedEntry(
            Text: translation,
            IsBaseForm: true,
            BaseFormText: null,
            GrammarLabel: null,
            Difficulty: null);

        var cloze = $"____ ({translation})";
        var sentenceText = $"{sourceEntry.Text} ({translation})";

        var context = new EnrichedContext(
            SourceText: sentenceText,
            SourceCloze: cloze,
            TargetText: translation,
            SourceFormText: sourceEntry.Text,
            TargetFormText: translation,
            Difficulty: null);

        return new EnrichmentResult([sourceEntry], [targetEntry], [context]);
    }
}
