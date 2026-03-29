using Langoose.Api.Infrastructure;
using Langoose.Api.Services;

namespace Langoose.Api.Tests.Infrastructure;

internal sealed class TestAppContext : IAsyncDisposable
{
    private TestAppContext(
        InMemoryDataStore dataStore,
        EnrichmentService enrichmentService,
        DictionaryService dictionaryService,
        StudyService studyService)
    {
        DataStore = dataStore;
        EnrichmentService = enrichmentService;
        DictionaryService = dictionaryService;
        StudyService = studyService;
    }

    public InMemoryDataStore DataStore { get; }
    public EnrichmentService EnrichmentService { get; }
    public DictionaryService DictionaryService { get; }
    public StudyService StudyService { get; }

    public static async Task<TestAppContext> CreateAsync()
    {
        var dataStore = new InMemoryDataStore();
        var seeder = new DataSeeder(dataStore);
        await seeder.SeedAsync();

        var enrichmentService = new EnrichmentService();
        var dictionaryService = new DictionaryService(dataStore, enrichmentService);
        var studyService = new StudyService(dataStore);

        return new TestAppContext(dataStore, enrichmentService, dictionaryService, studyService);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
