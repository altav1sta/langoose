using Langoose.Api.Infrastructure;
using Langoose.Api.Services;
using Microsoft.Extensions.Configuration;

namespace Langoose.Api.Tests.Infrastructure;

internal sealed class TestAppContext : IAsyncDisposable
{
    private TestAppContext(
        string root,
        FileDataStore dataStore,
        EnrichmentService enrichmentService,
        DictionaryService dictionaryService,
        StudyService studyService)
    {
        Root = root;
        DataStore = dataStore;
        EnrichmentService = enrichmentService;
        DictionaryService = dictionaryService;
        StudyService = studyService;
    }

    public string Root { get; }
    public FileDataStore DataStore { get; }
    public EnrichmentService EnrichmentService { get; }
    public DictionaryService DictionaryService { get; }
    public StudyService StudyService { get; }

    public static async Task<TestAppContext> CreateAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "langoose-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:DataDirectory"] = root
            })
            .Build();

        var environment = new StubEnvironment(root);
        var dataStore = new FileDataStore(configuration, environment);
        var seeder = new DataSeeder(dataStore);
        await seeder.SeedAsync();

        var enrichmentService = new EnrichmentService();
        var dictionaryService = new DictionaryService(dataStore, enrichmentService);
        var studyService = new StudyService(dataStore);

        return new TestAppContext(root, dataStore, enrichmentService, dictionaryService, studyService);
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}
