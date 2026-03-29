using Langoose.Domain.Abstractions;
using Langoose.Api.Models;
using Langoose.Domain.Models;

namespace Langoose.Api.Services;

public sealed class ContentService(IDataStore dataStore, EnrichmentService enrichmentService)
{
    public EnrichmentResponse Enrich(EnrichmentRequest request) => enrichmentService.Enrich(request);

    public async Task ReportIssueAsync(Guid userId, ReportIssueRequest request, CancellationToken cancellationToken)
    {
        var store = await dataStore.LoadAsync(cancellationToken);
        store.ContentFlags.Add(new ContentFlag
        {
            UserId = userId,
            ItemId = request.ItemId,
            Reason = request.Reason.Trim(),
            Details = request.Details?.Trim() ?? string.Empty
        });

        var item = store.DictionaryItems.FirstOrDefault(candidate => candidate.Id == request.ItemId);

        if (item is not null)
        {
            item.Status = DictionaryItemStatus.Flagged;
        }

        await dataStore.SaveAsync(store, cancellationToken);
    }
}
