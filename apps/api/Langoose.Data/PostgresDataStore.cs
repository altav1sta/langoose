using Langoose.Domain.Abstractions;
using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Data;

public sealed class PostgresDataStore(IDbContextFactory<AppDbContext> dbContextFactory) : IDataStore
{
    public async Task<DataStore> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return new DataStore
        {
            Users = await dbContext.Users.AsNoTracking().ToListAsync(cancellationToken),
            SessionTokens = await dbContext.SessionTokens.AsNoTracking().ToListAsync(cancellationToken),
            DictionaryItems = await dbContext.DictionaryItems.AsNoTracking().ToListAsync(cancellationToken),
            ExampleSentences = await dbContext.ExampleSentences.AsNoTracking().ToListAsync(cancellationToken),
            ReviewStates = await dbContext.ReviewStates.AsNoTracking().ToListAsync(cancellationToken),
            StudyEvents = await dbContext.StudyEvents.AsNoTracking().ToListAsync(cancellationToken),
            Imports = await dbContext.ImportRecords.AsNoTracking().ToListAsync(cancellationToken),
            ContentFlags = await dbContext.ContentFlags.AsNoTracking().ToListAsync(cancellationToken)
        };
    }

    public async Task SaveAsync(DataStore store, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.ContentFlags.RemoveRange(dbContext.ContentFlags);
        dbContext.ImportRecords.RemoveRange(dbContext.ImportRecords);
        dbContext.StudyEvents.RemoveRange(dbContext.StudyEvents);
        dbContext.ReviewStates.RemoveRange(dbContext.ReviewStates);
        dbContext.ExampleSentences.RemoveRange(dbContext.ExampleSentences);
        dbContext.DictionaryItems.RemoveRange(dbContext.DictionaryItems);
        dbContext.SessionTokens.RemoveRange(dbContext.SessionTokens);
        dbContext.Users.RemoveRange(dbContext.Users);

        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Users.AddRangeAsync(store.Users, cancellationToken);
        await dbContext.SessionTokens.AddRangeAsync(store.SessionTokens, cancellationToken);
        await dbContext.DictionaryItems.AddRangeAsync(store.DictionaryItems, cancellationToken);
        await dbContext.ExampleSentences.AddRangeAsync(store.ExampleSentences, cancellationToken);
        await dbContext.ReviewStates.AddRangeAsync(store.ReviewStates, cancellationToken);
        await dbContext.StudyEvents.AddRangeAsync(store.StudyEvents, cancellationToken);
        await dbContext.ImportRecords.AddRangeAsync(store.Imports, cancellationToken);
        await dbContext.ContentFlags.AddRangeAsync(store.ContentFlags, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
