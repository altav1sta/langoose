using Langoose.Data;
using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Api.IntegrationTests.Infrastructure;

internal static class TestDataSnapshot
{
    public static async Task<TestAppState> LoadAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        return new TestAppState
        {
            DictionaryItems = await dbContext.DictionaryItems.ToListAsync(cancellationToken),
            ExampleSentences = await dbContext.ExampleSentences.ToListAsync(cancellationToken),
            ReviewStates = await dbContext.ReviewStates.ToListAsync(cancellationToken),
            StudyEvents = await dbContext.StudyEvents.ToListAsync(cancellationToken),
            Imports = await dbContext.ImportRecords.ToListAsync(cancellationToken),
            ContentFlags = await dbContext.ContentFlags.ToListAsync(cancellationToken)
        };
    }
}
