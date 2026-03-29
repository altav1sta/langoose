using System.Text.Json;
using System.Text.Json.Serialization;
using Langoose.Api.Infrastructure;
using Langoose.Api.Models;

namespace Langoose.Api.Tests.Infrastructure;

internal sealed class InMemoryDataStore : IDataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private DataStore _store = new();

    public async Task<DataStore> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            return Clone(_store);
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
            _store = Clone(store);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static DataStore Clone(DataStore store)
    {
        var json = JsonSerializer.Serialize(store, JsonOptions);
        return JsonSerializer.Deserialize<DataStore>(json, JsonOptions) ?? new DataStore();
    }
}
