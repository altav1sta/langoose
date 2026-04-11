using Langoose.Data;
using Langoose.Domain.Models;
using Langoose.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Core.Services;

public sealed class ContentService(AppDbContext dbContext) : IContentService
{
    public async Task ReportIssueAsync(Guid userId, ReportIssueInput input, CancellationToken cancellationToken)
    {
        var entry = await dbContext.DictionaryEntries
            .FirstOrDefaultAsync(e => e.Id == input.DictionaryEntryId, cancellationToken);

        if (entry is null)
        {
            return;
        }

        dbContext.ContentFlags.Add(new ContentFlag
        {
            Id = Guid.CreateVersion7(),
            DictionaryEntryId = input.DictionaryEntryId,
            Reason = input.Reason.Trim(),
            ReportedByUserId = userId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
