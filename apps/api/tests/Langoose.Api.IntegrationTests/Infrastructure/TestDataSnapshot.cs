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
            EntryTranslations = await dbContext.EntryTranslations.ToListAsync(cancellationToken),
            EntryContexts = await dbContext.EntryContexts.ToListAsync(cancellationToken),
            UserDictionaryEntries = await dbContext.UserDictionaryEntries.ToListAsync(cancellationToken),
            UserProgress = await dbContext.UserProgress.ToListAsync(cancellationToken),
            StudyEvents = await dbContext.StudyEvents.ToListAsync(cancellationToken),
            ImportRecords = await dbContext.ImportRecords.ToListAsync(cancellationToken),
            ContentFlags = await dbContext.ContentFlags.ToListAsync(cancellationToken)
        };
    }
}
