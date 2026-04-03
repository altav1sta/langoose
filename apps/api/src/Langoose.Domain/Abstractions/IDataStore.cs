using Langoose.Domain.Models;

namespace Langoose.Domain.Abstractions;

public interface IDataStore
{
    Task<DataStore> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(DataStore store, CancellationToken cancellationToken = default);
}
