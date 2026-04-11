using Langoose.Domain.Models;

namespace Langoose.Domain.Services;

public interface IDictionaryService
{
    Task<IReadOnlyList<DictionaryItem>> GetItemsAsync(Guid userId, CancellationToken cancellationToken);

    Task<DictionaryItem> AddItemAsync(Guid userId, AddItemInput input, CancellationToken cancellationToken);

    Task<DictionaryItem?> PatchItemAsync(Guid userId, Guid itemId, PatchItemInput input, CancellationToken cancellationToken);

    Task<ImportResult> ImportCsvAsync(Guid userId, string csvContent, string fileName, CancellationToken cancellationToken);

    Task<string> ExportCsvAsync(Guid userId, CancellationToken cancellationToken);

    Task ClearCustomDataAsync(Guid userId, CancellationToken cancellationToken);
}
