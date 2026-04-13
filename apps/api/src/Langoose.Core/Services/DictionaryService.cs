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
    private static readonly string[] RequiredCsvHeaders =
        ["english term", "russian translation s", "part of speech"];
    private static readonly string[] LegacyCsvHeaders =
        ["english term", "russian translation s", "type"];
    private static readonly string[] OptionalCsvHeaders = ["notes", "tags"];

    public async Task<IReadOnlyList<DictionaryListItem>> GetVisibleEntriesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        // User's own entries (pending, enriched, failed, etc.)
        var userItems = await dbContext.UserDictionaryEntries
            .Where(x => x.UserId == userId)
            .Select(x => new DictionaryListItem(
                x.SourceEntryId ?? x.Id,
                x.SourceEntry != null ? x.SourceEntry.Text : x.UserInputTranslation ?? x.UserInputTerm,
                x.TargetLanguage,
                x.SourceEntry != null ? x.SourceEntry.Difficulty : null,
                false,
                x.Id,
                x.EnrichmentStatus,
                x.PartOfSpeech,
                x.Notes,
                x.Tags))
            .ToListAsync(cancellationToken);

        var userLinkedEntryIds = userItems
            .Where(x => !x.IsPublic)
            .Select(x => x.DictionaryEntryId)
            .ToHashSet();

        // Public base entries not already covered by user entries
        var publicItems = await dbContext.DictionaryEntries
            .Where(x => x.IsPublic && x.BaseEntryId == null && !userLinkedEntryIds.Contains(x.Id))
            .Select(x => new DictionaryListItem(
                x.Id, x.Text, x.Language, x.Difficulty, true,
                null, null, x.PartOfSpeech, null, new List<string>()))
            .ToListAsync(cancellationToken);

        return [.. publicItems.Concat(userItems).OrderBy(x => x.Text)];
    }

    public async Task<UserDictionaryEntry> AddUserEntryAsync(
        Guid userId,
        AddUserEntryInput input,
        CancellationToken cancellationToken)
    {
        var cleanedTerm = TextNormalizer.CleanInput(input.UserInputTerm);
        var cleanedTranslation = input.UserInputTranslation is not null
            ? TextNormalizer.CleanInput(input.UserInputTranslation)
            : null;

        var userEntries = await dbContext.UserDictionaryEntries
            .AsNoTracking()
            .Where(x => x.UserId == userId
                && x.SourceLanguage == input.SourceLanguage
                && x.PartOfSpeech == input.PartOfSpeech
                && x.UserInputTerm == cleanedTerm)
            .ToListAsync(cancellationToken);

        var existing = userEntries.FirstOrDefault(x =>
            x.TargetLanguage == input.TargetLanguage
            && x.UserInputTranslation == cleanedTranslation);

        if (existing is not null)
            return existing;

        var entry = new UserDictionaryEntry
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SourceLanguage = input.SourceLanguage,
            TargetLanguage = input.TargetLanguage,
            UserInputTerm = cleanedTerm,
            UserInputTranslation = cleanedTranslation,
            PartOfSpeech = input.PartOfSpeech,
            EnrichmentStatus = EnrichmentStatus.Pending,
            Notes = input.Notes,
            Tags = [.. input.Tags ?? []],
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
        var rows = csvContent.Split(
            ["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        if (rows.Length == 0)
        {
            throw new ArgumentException("CSV file is empty.");
        }

        ValidateCsvHeader(rows[0]);

        List<string> errors = [];
        List<(string Term, string Translation, string POS,
            string Notes, List<string> Tags)> candidates = [];

        foreach (var row in rows.Skip(1))
        {
            var columns = ParseCsvRow(row);

            if (columns.Count < 3 || columns.Count > 5)
            {
                errors.Add($"Malformed row: {row}");
                continue;
            }

            var englishTerm = TextNormalizer.CleanInput(columns[0]);
            var russianTranslation = TextNormalizer.CleanInput(columns[1]);
            var pos = TextNormalizer.CleanInput(columns[2]);
            var notes = columns.Count > 3
                ? TextNormalizer.CleanInput(columns[3])
                : "";
            var tags = columns.Count > 4
                ? SplitPipeValues(columns[4])
                : [];

            if (string.IsNullOrWhiteSpace(englishTerm))
            {
                errors.Add($"Missing required fields: {row}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(pos))
                pos = "noun";

            var term = string.IsNullOrWhiteSpace(russianTranslation)
                ? englishTerm
                : russianTranslation;
            var translation = englishTerm;
            candidates.Add((term, translation, pos, notes, tags));
        }

        if (errors.Count > 0)
        {
            return new ImportResult(
                Math.Max(0, rows.Length - 1), 0, errors);
        }

        var existingKeys = (await dbContext.UserDictionaryEntries
            .Where(x => x.UserId == userId)
            .Select(x => new { x.UserInputTerm, x.UserInputTranslation, x.PartOfSpeech })
            .ToListAsync(cancellationToken))
            .Select(x => (x.UserInputTerm, x.UserInputTranslation ?? "", x.PartOfSpeech))
            .ToHashSet();

        var pendingCount = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var (term, translation, pos, notes, tags) in candidates)
        {
            var cleanedTerm = TextNormalizer.CleanInput(term);
            var cleanedTranslation = string.IsNullOrWhiteSpace(translation)
                ? ""
                : TextNormalizer.CleanInput(translation);

            if (!existingKeys.Add((cleanedTerm, cleanedTranslation, pos)))
                continue;

            dbContext.UserDictionaryEntries.Add(new UserDictionaryEntry
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                SourceLanguage = "ru",
                TargetLanguage = "en",
                UserInputTerm = cleanedTerm,
                UserInputTranslation = cleanedTranslation.Length > 0
                    ? cleanedTranslation
                    : null,
                PartOfSpeech = pos,
                EnrichmentStatus = EnrichmentStatus.Pending,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

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

        return new ImportResult(
            Math.Max(0, rows.Length - 1), pendingCount, []);
    }

    public async Task<string> ExportCsvAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var entries = await dbContext.UserDictionaryEntries
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Include(x => x.SourceEntry)
            .ThenInclude(x => x!.Translations)
            .OrderBy(x => x.UserInputTerm)
            .ToListAsync(cancellationToken);

        List<string> rows =
            ["English term,Russian translation(s),Part of Speech,Notes,Tags"];

        foreach (var entry in entries)
        {
            var englishText = entry.SourceEntry?.Text
                ?? entry.UserInputTranslation
                ?? entry.UserInputTerm;

            var russianText = entry.SourceEntry?.Translations
                .Where(x => x.Language == "ru")
                .Select(x => x.Text)
                .FirstOrDefault()
                ?? entry.UserInputTerm;

            rows.Add(string.Join(
                ",",
                EscapeCsv(englishText),
                EscapeCsv(russianText),
                entry.PartOfSpeech,
                EscapeCsv(entry.Notes ?? ""),
                EscapeCsv(string.Join('|', entry.Tags))));
        }

        return string.Join(Environment.NewLine, rows);
    }

    public async Task ClearUserDataAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        var userEntries = await dbContext.UserDictionaryEntries
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        var publicEntryIds = await dbContext.DictionaryEntries
            .Where(x => x.IsPublic)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var publicEntryIdSet = publicEntryIds.ToHashSet();

        var userProgress = await dbContext.UserProgress
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        var customProgress = userProgress
            .Where(x => !publicEntryIdSet.Contains(x.DictionaryEntryId))
            .ToList();

        var studyEvents = await dbContext.StudyEvents
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        var customStudyEvents = studyEvents
            .Where(x => !publicEntryIdSet.Contains(x.DictionaryEntryId))
            .ToList();

        var imports = await dbContext.ImportRecords
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        var userFlags = await dbContext.ContentFlags
            .Where(x => x.ReportedByUserId == userId)
            .ToListAsync(cancellationToken);

        dbContext.UserDictionaryEntries.RemoveRange(userEntries);
        dbContext.UserProgress.RemoveRange(customProgress);
        dbContext.StudyEvents.RemoveRange(customStudyEvents);
        dbContext.ImportRecords.RemoveRange(imports);
        dbContext.ContentFlags.RemoveRange(userFlags);

        await dbContext.SaveChangesAsync(cancellationToken);
    }




    private static void ValidateCsvHeader(string headerRow)
    {
        List<string> headers = [.. ParseCsvRow(headerRow)
            .Select(x => TextNormalizer.NormalizeForComparison(x))];

        if (headers.Count < 3 || headers.Count > 5)
        {
            throw new ArgumentException(
                "CSV header must contain 3 to 5 columns: English term, "
                + "Russian translation(s), Part of Speech, "
                + "optional Notes, optional Tags.");
        }

        // Accept both "Part of Speech" and "Type" as third header
        var isCurrentFormat =
            headers.Take(3).SequenceEqual(RequiredCsvHeaders);
        var isLegacyFormat =
            headers.Take(3).SequenceEqual(LegacyCsvHeaders);

        if (!isCurrentFormat && !isLegacyFormat)
        {
            throw new ArgumentException(
                "CSV header must start with: English term, "
                + "Russian translation(s), Part of Speech.");
        }

        for (var index = RequiredCsvHeaders.Length;
             index < headers.Count;
             index++)
        {
            if (!OptionalCsvHeaders.Contains(
                    headers[index], StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    "Only Notes and Tags are allowed "
                    + "as optional CSV columns.");
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
            .Where(x => !string.IsNullOrWhiteSpace(x))];
    }
}
