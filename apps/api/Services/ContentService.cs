using Langoose.Api.Infrastructure;
using Langoose.Api.Models;

namespace Langoose.Api.Services;

public sealed class ContentService
{
    private readonly IDataStore _dataStore;
    private readonly EnrichmentService _enrichmentService;

    public ContentService(IDataStore dataStore, EnrichmentService enrichmentService)
    {
        _dataStore = dataStore;
        _enrichmentService = enrichmentService;
    }

    public EnrichmentResponse Enrich(EnrichmentRequest request) => _enrichmentService.Enrich(request);

    public async Task ReportIssueAsync(Guid userId, ReportIssueRequest request, CancellationToken cancellationToken)
    {
        var store = await _dataStore.LoadAsync(cancellationToken);
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

        await _dataStore.SaveAsync(store, cancellationToken);
    }
}