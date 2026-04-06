using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Data.Seeding;

public sealed class DatabaseSeeder(AppDbContext dbContext)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var hasDictionaryItems = await dbContext.DictionaryItems.AnyAsync(cancellationToken);

        if (hasDictionaryItems)
        {
            return;
        }

        var seedItems = SeedDataLoader.LoadBaseItems();

        foreach (var (item, sentence) in seedItems)
        {
            dbContext.DictionaryItems.Add(item);
            dbContext.ExampleSentences.Add(sentence);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
