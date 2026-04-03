using Langoose.Api.Models;
using Langoose.Data;
using Langoose.Domain.Enums;
using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Api.Services;

public sealed class ContentService(
    AppDbContext dbContext,
    EnrichmentService enrichmentService)
{
    public EnrichmentResponse Enrich(EnrichmentRequest request) => enrichmentService.Enrich(request);

    public async Task ReportIssueAsync(Guid userId, ReportIssueRequest request, CancellationToken cancellationToken)
    {
        dbContext.ContentFlags.Add(new ContentFlag
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ItemId = request.ItemId,
            Reason = request.Reason.Trim(),
            Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        var item = await dbContext.DictionaryItems.FirstOrDefaultAsync(
            x => x.Id == request.ItemId,
            cancellationToken);

        if (item is not null)
        {
            item.Status = DictionaryItemStatus.Flagged;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
