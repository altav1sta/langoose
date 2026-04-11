using Langoose.Core.Services;
using Langoose.Data;
using Langoose.Data.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Api.IntegrationTests.Infrastructure;

internal static class TestAppSetup
{
    public static async Task<TestDbContextFactory> CreateSeededDbContextFactoryAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"langoose-tests-{Guid.NewGuid():N}")
            .Options;
        var dbContextFactory = new TestDbContextFactory(options);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var seeder = new DatabaseSeeder(dbContext);
        await seeder.SeedAsync();

        return dbContextFactory;
    }

    public static DictionaryService CreateDictionaryService(AppDbContext dbContext)
        => new(dbContext);

    public static StudyService CreateStudyService(AppDbContext dbContext)
        => new(dbContext);
}
