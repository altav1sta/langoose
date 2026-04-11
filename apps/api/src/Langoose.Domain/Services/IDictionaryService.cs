using Langoose.Domain.Models;

namespace Langoose.Domain.Services;

public interface IDictionaryService
{
    Task<IReadOnlyList<DictionaryListItem>> GetVisibleEntriesAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserDictionaryEntry> AddUserEntryAsync(Guid userId, AddUserEntryInput input, CancellationToken cancellationToken);

    Task<ImportResult> ImportCsvAsync(Guid userId, string csvContent, string fileName, CancellationToken cancellationToken);

    Task<string> ExportCsvAsync(Guid userId, CancellationToken cancellationToken);

    Task ClearUserDataAsync(Guid userId, CancellationToken cancellationToken);
}
