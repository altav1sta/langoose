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

    public async Task<IReadOnlyList<UserDictionaryEntry>> GetUserEntriesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.UserDictionaryEntries
            .Where(e => e.UserId == userId)
            .Include(e => e.DictionaryEntry)
            .OrderBy(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserDictionaryEntry> AddUserEntryAsync(
        Guid userId,
        AddUserEntryInput input,
        CancellationToken cancellationToken)
    {
        var cleanedTerm = TextNormalizer.CleanInput(input.UserInputTerm);
        var type = input.Type ?? (cleanedTerm.Contains(' ') ? "phrase" : "word");

        // TODO: #71 — implement form-based dedup lookup via DictionaryEntry
        var entry = new UserDictionaryEntry
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SourceLanguage = input.SourceLanguage,
            TargetLanguage = input.TargetLanguage,
            UserInputTerm = cleanedTerm,
            EnrichmentStatus = EnrichmentStatus.Pending,
            Notes = input.Notes,
            Tags = input.Tags is not null ? [.. input.Tags] : [],
            Type = type,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.UserDictionaryEntries.Add(entry);
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

        var pendingCount = 0;

        foreach (var (term, notes, tags, type) in candidates)
        {
            var entry = new UserDictionaryEntry
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                SourceLanguage = "ru",
                TargetLanguage = "en",
                UserInputTerm = term,
                EnrichmentStatus = EnrichmentStatus.Pending,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                Tags = tags,
                Type = type,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            dbContext.UserDictionaryEntries.Add(entry);
            pendingCount++;
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

        var userProgress = await dbContext.UserProgress
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        var studyEvents = await dbContext.StudyEvents
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken);

        var imports = await dbContext.ImportRecords
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);

        dbContext.UserDictionaryEntries.RemoveRange(userEntries);
        dbContext.UserProgress.RemoveRange(userProgress);
        dbContext.StudyEvents.RemoveRange(studyEvents);
        dbContext.ImportRecords.RemoveRange(imports);

        await dbContext.SaveChangesAsync(cancellationToken);
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
