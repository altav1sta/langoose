using Langoose.Api.Models;

namespace Langoose.Api.Infrastructure;

public interface IDataStore
{
    Task<DataStore> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(DataStore store, CancellationToken cancellationToken = default);
}
