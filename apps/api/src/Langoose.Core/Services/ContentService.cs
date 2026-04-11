using Langoose.Data;
using Langoose.Domain.Enums;
using Langoose.Domain.Models;
using Langoose.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Core.Services;

public sealed class ContentService(
    AppDbContext dbContext,
    IEnrichmentService enrichmentService) : IContentService
{
    public EnrichmentResult Enrich(EnrichmentInput input) => enrichmentService.Enrich(input);

    public async Task ReportIssueAsync(Guid userId, ReportIssueInput input, CancellationToken cancellationToken)
    {
        dbContext.ContentFlags.Add(new ContentFlag
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ItemId = input.ItemId,
            Reason = input.Reason.Trim(),
            Details = string.IsNullOrWhiteSpace(input.Details) ? null : input.Details.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        var item = await dbContext.DictionaryItems.FirstOrDefaultAsync(
            x => x.Id == input.ItemId,
            cancellationToken);

        if (item is not null)
        {
            item.Status = DictionaryItemStatus.Flagged;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
