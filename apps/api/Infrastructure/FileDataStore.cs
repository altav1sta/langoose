using Langoose.Api.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Langoose.Api.Infrastructure;

public sealed class FileDataStore : IDataStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FileDataStore(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _filePath = BuildFilePath(configuration, environment);
    }

    public async Task<DataStore> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!File.Exists(_filePath))
            {
                return new DataStore();
            }

            await using var stream = File.OpenRead(_filePath);

            return await JsonSerializer.DeserializeAsync<DataStore>(
                       stream,
                       _jsonOptions,
                       cancellationToken)
                   ?? new DataStore();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(DataStore store, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, store, _jsonOptions, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string BuildFilePath(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var dataDirectory = configuration["Storage:DataDirectory"] ?? "App_Data";
        var root = Path.IsPathRooted(dataDirectory)
            ? dataDirectory
            : Path.Combine(environment.ContentRootPath, dataDirectory);

        Directory.CreateDirectory(root);

        return Path.Combine(root, "store.json");
    }
}
