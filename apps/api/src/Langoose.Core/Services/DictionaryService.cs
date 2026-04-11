using System.Text;
using Langoose.Core.Utilities;
using Langoose.Data;
using Langoose.Domain.Enums;
using Langoose.Domain.Models;
using Langoose.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Core.Services;

public sealed class DictionaryService(AppDbContext dbContext) : IDictionaryService
{
    private static readonly string[] RequiredCsvHeaders = ["english term", "russian translation s", "type"];
    private static readonly string[] OptionalCsvHeaders = ["notes", "tags"];

    public async Task<IReadOnlyList<DictionaryListItem>> GetVisibleEntriesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var publicEntries = await dbContext.DictionaryEntries
            .Where(e => e.IsPublic && e.IsBaseForm)
            .OrderBy(e => e.Text)
            .Select(e => new DictionaryListItem(
                e.Id, e.Text, e.Language, e.Difficulty, true,
                null, null, null, null, new List<string>()))
            .ToListAsync(cancellationToken);

        var userEntries = await dbContext.UserDictionaryEntries
            .Where(e => e.UserId == userId)
            .Include(e => e.DictionaryEntry)
            .OrderBy(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var userItems = userEntries.Select(e => new DictionaryListItem(
            e.DictionaryEntryId ?? e.Id,
            e.DictionaryEntry?.Text ?? e.UserInputTerm,
            e.TargetLanguage,
            e.DictionaryEntry?.Difficulty,
            false,
            e.Id,
            e.EnrichmentStatus,
            e.Type,
            e.Notes,
            e.Tags));

        // User entries linked to a public base entry replace the public row
        var userLinkedEntryIds = userEntries
            .Where(e => e.DictionaryEntryId is not null)
            .Select(e => e.DictionaryEntryId!.Value)
            .ToHashSet();

        var baseOnly = publicEntries.Where(e => !userLinkedEntryIds.Contains(e.DictionaryEntryId));

        return [.. baseOnly.Concat(userItems).OrderBy(e => e.Text)];
    }

    public async Task<UserDictionaryEntry> AddUserEntryAsync(
        Guid userId,
        AddUserEntryInput input,
        CancellationToken cancellationToken)
    {
        var userEntries = await dbContext.UserDictionaryEntries
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken);
        var publicBaseEntries = await dbContext.DictionaryEntries
            .Where(e => e.IsPublic && e.IsBaseForm)
            .ToListAsync(cancellationToken);

        var (entry, _) = UpsertUserEntry(
            dbContext,
            userId,
            input.UserInputTerm,
            input.SourceLanguage,
            input.TargetLanguage,
            input.Notes,
            input.Tags ?? [],
            input.Type,
            userEntries,
            publicBaseEntries);

        await dbContext.SaveChangesAsync(cancellationToken);

        return entry;
    }

    public async Task<ImportResult> ImportCsvAsync(
        Guid userId,
        string csvContent,
        string fileName,
        CancellationToken cancellationToken)
    {
        var rows = csvContent.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        if (rows.Length == 0)
        {
            throw new ArgumentException("CSV file is empty.");
        }

        ValidateCsvHeader(rows[0]);

        List<string> errors = [];
        List<(string Term, string Notes, List<string> Tags, string Type)> candidates = [];

        foreach (var row in rows.Skip(1))
        {
            var columns = ParseCsvRow(row);

            if (columns.Count < 3 || columns.Count > 5)
            {
                errors.Add($"Malformed row: {row}");
                continue;
            }

            var term = TextNormalizer.CleanInput(columns[0]);
            var type = TextNormalizer.CleanInput(columns[2]);
            var notes = columns.Count > 3 ? TextNormalizer.CleanInput(columns[3]) : "";
            var tags = columns.Count > 4 ? SplitPipeValues(columns[4]) : [];

            if (string.IsNullOrWhiteSpace(term))
            {
                errors.Add($"Missing required fields: {row}");
                continue;
            }

            candidates.Add((term, notes, tags, type));
        }

        if (errors.Count > 0)
        {
            return new ImportResult(Math.Max(0, rows.Length - 1), 0, errors);
        }

        var userEntries = await dbContext.UserDictionaryEntries
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken);
        var publicBaseEntries = await dbContext.DictionaryEntries
            .Where(e => e.IsPublic && e.IsBaseForm)
            .ToListAsync(cancellationToken);

        var pendingCount = 0;

        foreach (var (term, notes, tags, type) in candidates)
        {
            var (_, created) = UpsertUserEntry(
                dbContext,
                userId,
                term,
                "ru",
                "en",
                string.IsNullOrWhiteSpace(notes) ? null : notes,
                tags,
                type,
                userEntries,
                publicBaseEntries);

            if (created)
            {
                pendingCount++;
            }
        }

        dbContext.ImportRecords.Add(new ImportRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            RowCount = Math.Max(0, rows.Length - 1),
            PendingCount = pendingCount,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ImportResult(Math.Max(0, rows.Length - 1), pendingCount, []);
    }

    public async Task<string> ExportCsvAsync(Guid userId, CancellationToken cancellationToken)
    {
        // TODO: #71 — join with DictionaryEntry + EntryTranslation for full export
        var entries = await dbContext.UserDictionaryEntries
            .Where(e => e.UserId == userId && e.DictionaryEntryId != null)
            .Include(e => e.DictionaryEntry)
            .OrderBy(e => e.UserInputTerm)
            .ToListAsync(cancellationToken);

        List<string> rows = ["English term,Russian translation(s),Type,Notes,Tags"];

        foreach (var entry in entries)
        {
            rows.Add(string.Join(
                ",",
                EscapeCsv(entry.UserInputTerm),
                EscapeCsv(""),
                entry.Type ?? "word",
                EscapeCsv(entry.Notes ?? ""),
                EscapeCsv(string.Join('|', entry.Tags))));
        }

        return string.Join(Environment.NewLine, rows);
    }

    public async Task ClearUserDataAsync(Guid userId, CancellationToken cancellationToken)
    {
        var userEntries = await dbContext.UserDictionaryEntries
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken);

        var publicEntryIds = await dbContext.DictionaryEntries
            .Where(e => e.IsPublic)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
        var publicEntryIdSet = publicEntryIds.ToHashSet();

        var userProgress = await dbContext.UserProgress
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);
        var customProgress = userProgress
            .Where(p => !publicEntryIdSet.Contains(p.DictionaryEntryId))
            .ToList();

        var studyEvents = await dbContext.StudyEvents
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken);
        var customStudyEvents = studyEvents
            .Where(e => !publicEntryIdSet.Contains(e.DictionaryEntryId))
            .ToList();

        var imports = await dbContext.ImportRecords
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);

        var userFlags = await dbContext.ContentFlags
            .Where(f => f.ReportedByUserId == userId)
            .ToListAsync(cancellationToken);

        dbContext.UserDictionaryEntries.RemoveRange(userEntries);
        dbContext.UserProgress.RemoveRange(customProgress);
        dbContext.StudyEvents.RemoveRange(customStudyEvents);
        dbContext.ImportRecords.RemoveRange(imports);
        dbContext.ContentFlags.RemoveRange(userFlags);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static (UserDictionaryEntry Entry, bool Created) UpsertUserEntry(
        AppDbContext dbContext,
        Guid userId,
        string rawTerm,
        string sourceLanguage,
        string targetLanguage,
        string? notes,
        List<string> tags,
        string? type,
        List<UserDictionaryEntry> userEntries,
        List<DictionaryEntry> publicBaseEntries)
    {
        var cleanedTerm = TextNormalizer.CleanInput(rawTerm);
        var normalizedTerm = TextNormalizer.NormalizeForComparison(cleanedTerm);
        var resolvedType = type ?? (cleanedTerm.Contains(' ') ? "phrase" : "word");

        // 1. Check if user already has a UserDictionaryEntry for this term → merge
        var existingUserEntry = userEntries.FirstOrDefault(e =>
            TextNormalizer.NormalizeForComparison(e.UserInputTerm) == normalizedTerm);

        if (existingUserEntry is not null)
        {
            MergeUserEntry(existingUserEntry, notes, tags);

            return (existingUserEntry, false);
        }

        // 2. Check if a public base DictionaryEntry matches → link immediately
        var matchingBaseEntry = publicBaseEntries.FirstOrDefault(e =>
            e.Language == targetLanguage &&
            TextNormalizer.NormalizeForComparison(e.Text) == normalizedTerm);

        var entry = new UserDictionaryEntry
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            DictionaryEntryId = matchingBaseEntry?.Id,
            SourceLanguage = sourceLanguage,
            TargetLanguage = targetLanguage,
            UserInputTerm = cleanedTerm,
            EnrichmentStatus = matchingBaseEntry is not null
                ? EnrichmentStatus.Enriched
                : EnrichmentStatus.Pending,
            Notes = notes,
            Tags = [.. tags],
            Type = resolvedType,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.UserDictionaryEntries.Add(entry);
        userEntries.Add(entry);

        return (entry, true);
    }

    private static void MergeUserEntry(
        UserDictionaryEntry existing,
        string? notes,
        List<string> tags)
    {
        if (tags.Count > 0)
        {
            existing.Tags = [.. existing.Tags
                .Concat(tags)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(TextNormalizer.CleanInput)
                .Distinct(StringComparer.OrdinalIgnoreCase)];
        }

        if (!string.IsNullOrWhiteSpace(notes) &&
            !(existing.Notes ?? "").Contains(notes, StringComparison.OrdinalIgnoreCase))
        {
            existing.Notes = string.IsNullOrWhiteSpace(existing.Notes)
                ? notes
                : $"{existing.Notes}; {notes}";
        }

        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static void ValidateCsvHeader(string headerRow)
    {
        List<string> headers = [.. ParseCsvRow(headerRow)
            .Select(header => TextNormalizer.NormalizeForComparison(header))];

        if (headers.Count < 3 || headers.Count > 5)
        {
            throw new ArgumentException(
                "CSV header must contain 3 to 5 columns: English term, " +
                "Russian translation(s), Type, optional Notes, optional Tags.");
        }

        for (var index = 0; index < RequiredCsvHeaders.Length; index++)
        {
            if (headers.Count <= index || headers[index] != RequiredCsvHeaders[index])
            {
                throw new ArgumentException(
                    "CSV header must start with: English term, Russian translation(s), Type.");
            }
        }

        for (var index = RequiredCsvHeaders.Length; index < headers.Count; index++)
        {
            if (!OptionalCsvHeaders.Contains(headers[index], StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    "Only Notes and Tags are allowed as optional CSV columns.");
            }
        }
    }

    private static List<string> ParseCsvRow(string row)
    {
        List<string> result = [];
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var ch in row)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        result.Add(current.ToString());

        return result;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static List<string> SplitPipeValues(string value)
    {
        return [.. value
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(TextNormalizer.CleanInput)
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))];
    }
}
