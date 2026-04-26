using Langoose.Data;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Api.IntegrationTests.Infrastructure;

internal static class TestDataSnapshot
{
    public static async Task<TestAppState> LoadAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        return new TestAppState
        {
            DictionaryEntries = await dbContext.DictionaryEntries.ToListAsync(cancellationToken),
            EntryContexts = await dbContext.EntryContexts.ToListAsync(cancellationToken),
            UserEntries = await dbContext.UserEntries.ToListAsync(cancellationToken),
            UserProgress = await dbContext.UserProgress.ToListAsync(cancellationToken),
            StudyEvents = await dbContext.StudyEvents.ToListAsync(cancellationToken),
            UserImports = await dbContext.UserImports.ToListAsync(cancellationToken),
            ContentFlags = await dbContext.ContentFlags.ToListAsync(cancellationToken)
        };
    }
}
