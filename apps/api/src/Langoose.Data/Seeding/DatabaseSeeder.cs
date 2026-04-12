using Microsoft.EntityFrameworkCore;

namespace Langoose.Data.Seeding;

public sealed class DatabaseSeeder(AppDbContext dbContext)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.DictionaryEntries.AnyAsync(cancellationToken))
        {
            return;
        }

        var batch = SeedDataLoader.LoadBaseItems();

        dbContext.DictionaryEntries.AddRange(batch.Entries);
        dbContext.EntryTranslations.AddRange(batch.Translations);
        dbContext.EntryContexts.AddRange(batch.Contexts);
        dbContext.ContextTranslations.AddRange(batch.ContextTranslations);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
