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
            e.DictionaryEntry?.Text ?? e.UserInputTranslation ?? e.UserInputTerm,
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
        var (userEntries, allBaseEntries, allTranslations) =
            await LoadUpsertStateAsync(userId, cancellationToken);

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
            allBaseEntries,
            allTranslations);

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

        var (userEntries, allBaseEntries, allTranslations) =
            await LoadUpsertStateAsync(userId, cancellationToken);

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
                allBaseEntries,
                allTranslations);

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
            .Where(e => e.UserId == userId)
            .Include(e => e.DictionaryEntry)
            .OrderBy(e => e.UserInputTerm)
            .ToListAsync(cancellationToken);

        var enrichedEntryIds = entries
            .Where(e => e.DictionaryEntryId is not null)
            .Select(e => e.DictionaryEntryId!.Value)
            .ToHashSet();

        var translations = await dbContext.EntryTranslations
            .Where(t => enrichedEntryIds.Contains(t.SourceEntryId))
            .ToListAsync(cancellationToken);

        var targetEntryIds = translations.Select(t => t.TargetEntryId).ToHashSet();
        var targetEntries = await dbContext.DictionaryEntries
            .Where(e => targetEntryIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var translationsBySource = translations
            .GroupBy(t => t.SourceEntryId)
            .ToDictionary(g => g.Key, g => g.ToList());

        List<string> rows = ["English term,Russian translation(s),Type,Notes,Tags"];

        foreach (var entry in entries)
        {
            var englishText = entry.DictionaryEntry?.Text
                ?? entry.UserInputTranslation
                ?? entry.UserInputTerm;
            var russianText = ResolveTranslation(entry, translationsBySource, targetEntries, "ru");

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
        Dictionary<Guid, List<EntryTranslation>> translationsBySource,
        Dictionary<Guid, DictionaryEntry> targetEntries,
        string targetLanguage)
    {
        if (entry.DictionaryEntryId is not null &&
            translationsBySource.TryGetValue(entry.DictionaryEntryId.Value, out var links))
        {
            var translatedTexts = links
                .Where(t => targetEntries.TryGetValue(t.TargetEntryId, out var te) && te.Language == targetLanguage)
                .Select(t => targetEntries[t.TargetEntryId].Text)
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
        string? rawTranslation,
        string sourceLanguage,
        string targetLanguage,
        string? notes,
        List<string> tags,
        string? type,
        List<UserDictionaryEntry> userEntries,
        List<DictionaryEntry> allBaseEntries,
        List<EntryTranslation> allTranslations)
    {
        var cleanedTerm = TextNormalizer.CleanInput(rawTerm);
        var normalizedTerm = TextNormalizer.NormalizeForComparison(cleanedTerm);
        var cleanedTranslation = rawTranslation is not null
            ? TextNormalizer.CleanInput(rawTranslation)
            : null;
        var displayTerm = cleanedTranslation ?? cleanedTerm;
        var resolvedType = type ?? (displayTerm.Contains(' ') ? "phrase" : "word");

        // 1. Check if user already has a UserDictionaryEntry for this term → merge
        var existingUserEntry = userEntries.FirstOrDefault(e =>
            TextNormalizer.NormalizeForComparison(e.UserInputTerm) == normalizedTerm);

        if (existingUserEntry is not null)
        {
            MergeUserEntry(existingUserEntry, notes, tags);

            return (existingUserEntry, false);
        }

        // 2. Documented dedup path: source-language term → DictionaryEntry form →
        //    BaseEntryId → EntryTranslation → linked target-language base entry
        DictionaryEntry? linkedEntry = null;

        // Look up the source-language term (userInputTerm) as a DictionaryEntry form
        var sourceForm = allBaseEntries.FirstOrDefault(e =>
            e.Language == sourceLanguage &&
            TextNormalizer.NormalizeForComparison(e.Text) == normalizedTerm);

        // Follow BaseEntryId to the base form if this is a derived form
        var sourceBase = sourceForm is not null && !sourceForm.IsBaseForm
            ? allBaseEntries.FirstOrDefault(e => e.Id == sourceForm.BaseEntryId)
            : sourceForm;

        // Check EntryTranslation for a linked target-language entry
        if (sourceBase is not null)
        {
            var translationLink = allTranslations.FirstOrDefault(t =>
                t.SourceEntryId == sourceBase.Id);

            if (translationLink is not null)
            {
                linkedEntry = allBaseEntries.FirstOrDefault(e =>
                    e.Id == translationLink.TargetEntryId &&
                    e.Language == targetLanguage);
            }
        }

        // 3. Fallback: direct match on the target-language translation text
        if (linkedEntry is null && !string.IsNullOrWhiteSpace(cleanedTranslation))
        {
            var normalizedTranslation = TextNormalizer.NormalizeForComparison(cleanedTranslation);

            linkedEntry = allBaseEntries.FirstOrDefault(e =>
                e.Language == targetLanguage &&
                e.IsBaseForm &&
                TextNormalizer.NormalizeForComparison(e.Text) == normalizedTranslation);
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

    private async Task<(List<UserDictionaryEntry> UserEntries, List<DictionaryEntry> BaseEntries, List<EntryTranslation> Translations)>
        LoadUpsertStateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var userEntries = await dbContext.UserDictionaryEntries
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken);
        var allBaseEntries = await dbContext.DictionaryEntries
            .Where(e => e.IsPublic)
            .ToListAsync(cancellationToken);
        var allTranslations = await dbContext.EntryTranslations
            .ToListAsync(cancellationToken);

        return (userEntries, allBaseEntries, allTranslations);
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
