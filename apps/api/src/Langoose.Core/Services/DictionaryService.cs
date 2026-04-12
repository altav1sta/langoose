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
            .Where(x => x.IsPublic && x.BaseEntryId == null)
            .OrderBy(x => x.Text)
            .Select(x => new DictionaryListItem(
                x.Id, x.Text, x.Language, x.Difficulty, true,
                null, null, null, null, new List<string>()))
            .ToListAsync(cancellationToken);

        var userEntries = await dbContext.UserDictionaryEntries
            .Where(x => x.UserId == userId)
            .Include(x => x.DictionaryEntry)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var userItems = userEntries.Select(x => new DictionaryListItem(
            x.DictionaryEntryId ?? x.Id,
            x.DictionaryEntry?.Text ?? x.UserInputTranslation ?? x.UserInputTerm,
            x.TargetLanguage,
            x.DictionaryEntry?.Difficulty,
            false,
            x.Id,
            x.EnrichmentStatus,
            x.Type,
            x.Notes,
            x.Tags));

        // User entries linked to a public base entry replace the public row
        var userLinkedEntryIds = userEntries
            .Where(x => x.DictionaryEntryId is not null)
            .Select(x => x.DictionaryEntryId!.Value)
            .ToHashSet();

        var baseOnly = publicEntries.Where(x => !userLinkedEntryIds.Contains(x.DictionaryEntryId));

        return [.. baseOnly.Concat(userItems).OrderBy(x => x.Text)];
    }

    public async Task<UserDictionaryEntry> AddUserEntryAsync(
        Guid userId,
        AddUserEntryInput input,
        CancellationToken cancellationToken)
    {
        var (userEntries, allBaseEntries) = await LoadUpsertStateAsync(userId, cancellationToken);

        var (entry, _) = UpsertUserEntry(
            dbContext,
            userId,
            input.UserInputTerm,
            input.UserInputTranslation,
            input.SourceLanguage,
            input.TargetLanguage,
            input.Notes,
            input.Tags ?? [],
            input.Type,
            userEntries,
            allBaseEntries);

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
        List<(string Term, string Translation, string Notes, List<string> Tags, string Type)> candidates = [];

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
            var type = TextNormalizer.CleanInput(columns[2]);
            var notes = columns.Count > 3 ? TextNormalizer.CleanInput(columns[3]) : "";
            var tags = columns.Count > 4 ? SplitPipeValues(columns[4]) : [];

            if (string.IsNullOrWhiteSpace(englishTerm))
            {
                errors.Add($"Missing required fields: {row}");
                continue;
            }

            // userInputTerm = Russian (source), userInputTranslation = English (target)
            var term = string.IsNullOrWhiteSpace(russianTranslation) ? englishTerm : russianTranslation;
            var translation = englishTerm;
            candidates.Add((term, translation, notes, tags, type));
        }

        if (errors.Count > 0)
        {
            return new ImportResult(Math.Max(0, rows.Length - 1), 0, errors);
        }

        var (userEntries, allBaseEntries) = await LoadUpsertStateAsync(userId, cancellationToken);

        var pendingCount = 0;

        foreach (var (term, translation, notes, tags, type) in candidates)
        {
            var (_, created) = UpsertUserEntry(
                dbContext,
                userId,
                term,
                string.IsNullOrWhiteSpace(translation) ? null : translation,
                "ru",
                "en",
                string.IsNullOrWhiteSpace(notes) ? null : notes,
                tags,
                type,
                userEntries,
                allBaseEntries);

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
        var entries = await dbContext.UserDictionaryEntries
            .Where(x => x.UserId == userId)
            .Include(x => x.DictionaryEntry)
            .OrderBy(x => x.UserInputTerm)
            .ToListAsync(cancellationToken);

        var enrichedEntryIds = entries
            .Where(x => x.DictionaryEntryId is not null)
            .Select(x => x.DictionaryEntryId!.Value)
            .ToHashSet();

        var translationsBySource = await dbContext.DictionaryEntries
            .Where(x => enrichedEntryIds.Contains(x.Id))
            .Include(x => x.Translations)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        List<string> rows = ["English term,Russian translation(s),Type,Notes,Tags"];

        foreach (var entry in entries)
        {
            var englishText = entry.DictionaryEntry?.Text
                ?? entry.UserInputTranslation
                ?? entry.UserInputTerm;
            var russianText = ResolveTranslation(entry, translationsBySource, "ru");

            if (string.IsNullOrWhiteSpace(russianText))
            {
                russianText = entry.UserInputTerm;
            }

            rows.Add(string.Join(
                ",",
                EscapeCsv(englishText),
                EscapeCsv(russianText),
                entry.Type ?? "word",
                EscapeCsv(entry.Notes ?? ""),
                EscapeCsv(string.Join('|', entry.Tags))));
        }

        return string.Join(Environment.NewLine, rows);
    }

    private static string ResolveTranslation(
        UserDictionaryEntry entry,
        Dictionary<Guid, DictionaryEntry> sourceEntries,
        string targetLanguage)
    {
        if (entry.DictionaryEntryId is not null &&
            sourceEntries.TryGetValue(entry.DictionaryEntryId.Value, out var sourceEntry))
        {
            var translatedTexts = sourceEntry.Translations
                .Where(x => x.Language == targetLanguage)
                .Select(x => x.Text)
                .ToList();

            if (translatedTexts.Count > 0)
            {
                return string.Join('|', translatedTexts);
            }
        }

        return entry.UserInputTranslation ?? "";
    }

    public async Task ClearUserDataAsync(Guid userId, CancellationToken cancellationToken)
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

    private static (UserDictionaryEntry Entry, bool Created) UpsertUserEntry(
        AppDbContext dbContext,
        Guid userId,
        string rawTerm,
        string? rawTranslation,
        string sourceLanguage,
        string targetLanguage,
        string? notes,
        List<string> tags,
        string? type,
        List<UserDictionaryEntry> userEntries,
        List<DictionaryEntry> allBaseEntries)
    {
        var cleanedTerm = TextNormalizer.CleanInput(rawTerm);
        var normalizedTerm = TextNormalizer.NormalizeForComparison(cleanedTerm);
        var cleanedTranslation = rawTranslation is not null
            ? TextNormalizer.CleanInput(rawTranslation)
            : null;
        var displayTerm = cleanedTranslation ?? cleanedTerm;
        var resolvedType = type ?? (displayTerm.Contains(' ') ? "phrase" : "word");

        // 1. Check if user already has a UserDictionaryEntry for this term -> merge
        var existingUserEntry = userEntries.FirstOrDefault(x =>
            TextNormalizer.NormalizeForComparison(x.UserInputTerm) == normalizedTerm);

        if (existingUserEntry is not null)
        {
            MergeUserEntry(existingUserEntry, notes, tags);

            return (existingUserEntry, false);
        }

        // 2. Documented dedup path: source-language term -> DictionaryEntry form ->
        //    BaseEntryId -> Translations nav -> linked target-language base entry
        DictionaryEntry? linkedEntry = null;

        // Look up the source-language term (userInputTerm) as a DictionaryEntry form
        var sourceForm = allBaseEntries.FirstOrDefault(x =>
            x.Language == sourceLanguage &&
            TextNormalizer.NormalizeForComparison(x.Text) == normalizedTerm);

        // Follow BaseEntryId to the base form if this is a derived form
        var sourceBase = sourceForm is not null && sourceForm.BaseEntryId is not null
            ? allBaseEntries.FirstOrDefault(x => x.Id == sourceForm.BaseEntryId)
            : sourceForm;

        // Check Translations navigation for a linked target-language entry
        if (sourceBase is not null)
        {
            linkedEntry = sourceBase.Translations
                .FirstOrDefault(x => x.Language == targetLanguage);
        }

        // 3. Fallback: direct match on the target-language translation text
        if (linkedEntry is null && !string.IsNullOrWhiteSpace(cleanedTranslation))
        {
            var normalizedTranslation = TextNormalizer.NormalizeForComparison(cleanedTranslation);

            linkedEntry = allBaseEntries.FirstOrDefault(x =>
                x.Language == targetLanguage &&
                x.BaseEntryId is null &&
                TextNormalizer.NormalizeForComparison(x.Text) == normalizedTranslation);
        }

        var entry = new UserDictionaryEntry
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            DictionaryEntryId = linkedEntry?.Id,
            SourceLanguage = sourceLanguage,
            TargetLanguage = targetLanguage,
            UserInputTerm = cleanedTerm,
            UserInputTranslation = cleanedTranslation,
            EnrichmentStatus = linkedEntry is not null
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
                .Where(x => !string.IsNullOrWhiteSpace(x))
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

    private async Task<(List<UserDictionaryEntry> UserEntries, List<DictionaryEntry> BaseEntries)>
        LoadUpsertStateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var userEntries = await dbContext.UserDictionaryEntries
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        var allBaseEntries = await dbContext.DictionaryEntries
            .Where(x => x.IsPublic)
            .Include(x => x.Translations)
            .ToListAsync(cancellationToken);

        return (userEntries, allBaseEntries);
    }

    private static void ValidateCsvHeader(string headerRow)
    {
        List<string> headers = [.. ParseCsvRow(headerRow)
            .Select(x => TextNormalizer.NormalizeForComparison(x))];

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
            .Where(x => !string.IsNullOrWhiteSpace(x))];
    }
}
