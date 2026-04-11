using System.Text;
using Langoose.Core.Utilities;
using Langoose.Data;
using Langoose.Domain.Constants;
using Langoose.Domain.Enums;
using Langoose.Domain.Models;
using Langoose.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Core.Services;

public sealed class DictionaryService(
    AppDbContext dbContext,
    IEnrichmentService enrichmentService) : IDictionaryService
{
    private static readonly string[] RequiredCsvHeaders = ["english term", "russian translation s", "type"];
    private static readonly string[] OptionalCsvHeaders = ["notes", "tags"];

    public async Task<IReadOnlyList<DictionaryItem>> GetItemsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var state = await LoadTrackedStateAsync(dbContext, cancellationToken);

        if (NormalizeCustomDuplicates(dbContext, state, userId))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return [.. GetVisibleItems(state.DictionaryItems, userId)
            .OrderBy(item => item.SourceType)
            .ThenBy(item => item.EnglishText)];
    }

    public async Task<DictionaryItem> AddItemAsync(
        Guid userId,
        AddItemInput input,
        CancellationToken cancellationToken)
    {
        var state = await LoadTrackedStateAsync(dbContext, cancellationToken);
        var (item, _) = await UpsertItemAsync(dbContext, state, userId, input, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return item;
    }

    public async Task<DictionaryItem?> PatchItemAsync(
        Guid userId,
        Guid itemId,
        PatchItemInput input,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.DictionaryItems.FirstOrDefaultAsync(candidate =>
            candidate.Id == itemId &&
            candidate.OwnerId == userId, cancellationToken);

        if (item is null)
        {
            return null;
        }

        if (input.RussianGlosses is not null)
        {
            item.RussianGlosses = CleanValues(input.RussianGlosses);
        }

        if (!string.IsNullOrWhiteSpace(input.PartOfSpeech))
        {
            item.PartOfSpeech = TextNormalizer.CleanInput(input.PartOfSpeech);
        }

        if (!string.IsNullOrWhiteSpace(input.Difficulty))
        {
            item.Difficulty = TextNormalizer.CleanInput(input.Difficulty);
        }

        if (input.Notes is not null)
        {
            item.Notes = TextNormalizer.CleanInput(input.Notes);
        }

        if (input.Tags is not null)
        {
            item.Tags = CleanValues(input.Tags);
        }

        if (!string.IsNullOrWhiteSpace(input.Status) &&
            Enum.TryParse<DictionaryItemStatus>(input.Status, true, out var status))
        {
            item.Status = status;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return item;
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
        List<AddItemInput> candidates = [];

        foreach (var row in rows.Skip(1))
        {
            var columns = ParseCsvRow(row);

            if (columns.Count < 3 || columns.Count > 5)
            {
                errors.Add($"Malformed row: {row}");
                continue;
            }

            var english = TextNormalizer.CleanInput(columns[0]);
            var russianGlosses = SplitPipeValues(columns[1]);
            var type = TextNormalizer.CleanInput(columns[2]);
            var notes = columns.Count > 3
                ? TextNormalizer.CleanInput(columns[3])
                : "";
            var tags = columns.Count > 4 ? SplitPipeValues(columns[4]) : [];

            if (string.IsNullOrWhiteSpace(english) || russianGlosses.Count == 0)
            {
                errors.Add($"Missing required fields: {row}");
                continue;
            }

            candidates.Add(new AddItemInput(
                english,
                russianGlosses,
                type,
                null,
                null,
                notes,
                tags,
                "csv-import"));
        }

        if (errors.Count > 0)
        {
            return new ImportResult(
                Math.Max(0, rows.Length - 1),
                0,
                Math.Max(0, rows.Length - 1),
                errors);
        }

        var state = await LoadTrackedStateAsync(dbContext, cancellationToken);
        var imported = 0;
        var skipped = 0;

        foreach (var candidate in candidates)
        {
            var (_, created) = await UpsertItemAsync(dbContext, state, userId, candidate, cancellationToken);

            if (created)
            {
                imported++;
            }
            else
            {
                skipped++;
            }
        }

        var importRecord = new ImportRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileName = fileName,
            TotalRows = Math.Max(0, rows.Length - 1),
            ImportedRows = imported,
            SkippedRows = skipped,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        dbContext.ImportRecords.Add(importRecord);
        state.Imports.Add(importRecord);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ImportResult(Math.Max(0, rows.Length - 1), imported, skipped, []);
    }

    public async Task<string> ExportCsvAsync(Guid userId, CancellationToken cancellationToken)
    {
        var state = await LoadTrackedStateAsync(dbContext, cancellationToken);

        if (NormalizeCustomDuplicates(dbContext, state, userId))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        List<string> rows = ["English term,Russian translation(s),Type,Notes,Tags"];
        var visibleCustomItems = GetVisibleItems(state.DictionaryItems, userId)
            .Where(candidate => candidate.OwnerId == userId)
            .OrderBy(candidate => candidate.EnglishText);

        foreach (var item in visibleCustomItems)
        {
            rows.Add(string.Join(
                ",",
                EscapeCsv(item.EnglishText),
                EscapeCsv(string.Join('|', item.RussianGlosses)),
                item.ItemKind.ToString().ToLowerInvariant(),
                EscapeCsv(item.Notes),
                EscapeCsv(string.Join('|', item.Tags))));
        }

        return string.Join(Environment.NewLine, rows);
    }

    public async Task ClearCustomDataAsync(Guid userId, CancellationToken cancellationToken)
    {
        var customItems = await dbContext.DictionaryItems
            .Where(item => item.OwnerId == userId)
            .ToListAsync(cancellationToken);
        var customItemIds = customItems.Select(item => item.Id).ToHashSet();
        var customSentences = await dbContext.ExampleSentences
            .Where(sentence => customItemIds.Contains(sentence.ItemId))
            .ToListAsync(cancellationToken);
        var reviewStates = await dbContext.ReviewStates
            .Where(state => state.UserId == userId)
            .ToListAsync(cancellationToken);
        var studyEvents = await dbContext.StudyEvents
            .Where(ev => ev.UserId == userId)
            .ToListAsync(cancellationToken);
        var imports = await dbContext.ImportRecords
            .Where(importRecord => importRecord.UserId == userId)
            .ToListAsync(cancellationToken);
        var flags = await dbContext.ContentFlags
            .Where(flag => flag.UserId == userId || customItemIds.Contains(flag.ItemId))
            .ToListAsync(cancellationToken);

        dbContext.DictionaryItems.RemoveRange(customItems);
        dbContext.ExampleSentences.RemoveRange(customSentences);
        dbContext.ReviewStates.RemoveRange(reviewStates);
        dbContext.StudyEvents.RemoveRange(studyEvents);
        dbContext.ImportRecords.RemoveRange(imports);
        dbContext.ContentFlags.RemoveRange(flags);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<(DictionaryItem Item, bool Created)> UpsertItemAsync(
        AppDbContext dbContext,
        AppState state,
        Guid userId,
        AddItemInput input,
        CancellationToken cancellationToken)
    {
        NormalizeCustomDuplicates(dbContext, state, userId);

        var cleanedEnglish = TextNormalizer.CleanInput(input.EnglishText);
        var normalizedEnglish = TextNormalizer.NormalizeForComparison(cleanedEnglish);
        var itemKind = ResolveItemKind(input.ItemKind, cleanedEnglish);
        var existingCustom = state.DictionaryItems.FirstOrDefault(item =>
            item.OwnerId == userId &&
            TextNormalizer.NormalizeForComparison(item.EnglishText) == normalizedEnglish);
        var existingBase = state.DictionaryItems.FirstOrDefault(item =>
            item.OwnerId is null &&
            TextNormalizer.NormalizeForComparison(item.EnglishText) == normalizedEnglish);

        var enrichment = enrichmentService.Enrich(
            new EnrichmentInput(cleanedEnglish, input.RussianGlosses, input.ItemKind));
        var glosses = (input.RussianGlosses?.Count ?? 0) > 0
            ? input.RussianGlosses!
            : enrichment.RussianGlosses;
        var cleanedGlosses = CleanValues(glosses);
        var cleanedTags = CleanValues(input.Tags ?? []);
        var cleanedNotes = TextNormalizer.CleanInput(input.Notes ?? "");

        if (existingCustom is not null)
        {
            MergeIntoCustom(
                existingCustom,
                cleanedEnglish,
                cleanedGlosses,
                cleanedTags,
                cleanedNotes,
                itemKind,
                enrichment.AcceptedVariants,
                normalizedEnglish);

            return (existingCustom, false);
        }

        if (existingBase is not null)
        {
            var reviewState = state.ReviewStates.FirstOrDefault(reviewState =>
                reviewState.UserId == userId &&
                reviewState.ItemId == existingBase.Id);

            if (reviewState is null)
            {
                reviewState = new ReviewState
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ItemId = existingBase.Id,
                    Stability = ReviewDefaults.InitialStability,
                    DueAtUtc = DateTimeOffset.UtcNow
                };
                dbContext.ReviewStates.Add(reviewState);
                state.ReviewStates.Add(reviewState);
            }

            return (existingBase, false);
        }

        var item = new DictionaryItem
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            SourceType = SourceType.Custom,
            EnglishText = cleanedEnglish,
            RussianGlosses = [.. cleanedGlosses.Distinct(StringComparer.OrdinalIgnoreCase)],
            ItemKind = itemKind,
            PartOfSpeech = input.PartOfSpeech?.Trim() ?? enrichment.PartOfSpeech,
            Difficulty = input.Difficulty?.Trim() ?? enrichment.Difficulty,
            Status = DictionaryItemStatus.Active,
            Notes = cleanedNotes,
            Tags = cleanedTags,
            CreatedByFlow = input.CreatedByFlow?.Trim() ?? "quick-add",
            AcceptedVariants = [.. enrichment.AcceptedVariants
                .Append(cleanedEnglish)
                .Append(normalizedEnglish)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(TextNormalizer.CleanInput)
                .Distinct(StringComparer.OrdinalIgnoreCase)],
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        item.Distractors = item.ItemKind == ItemKind.Phrase
            ? ["make up", "look after", "find out"]
            : ["get", "make", "take"];

        dbContext.DictionaryItems.Add(item);
        state.DictionaryItems.Add(item);

        if (input.GenerateExamples)
        {
            var validExamples = enrichment.Examples.Where(candidate =>
                !enrichment.ValidationWarnings.Contains(
                    "Example sentence must include the target term."));

            foreach (var example in validExamples)
            {
                var sentence = new ExampleSentence
                {
                    Id = Guid.NewGuid(),
                    ItemId = item.Id,
                    SentenceText = example.SentenceText,
                    ClozeText = example.ClozeText,
                    TranslationHint = example.TranslationHint,
                    QualityScore = example.QualityScore,
                    Origin = ContentOrigin.Manual
                };

                dbContext.ExampleSentences.Add(sentence);
                state.ExampleSentences.Add(sentence);
            }
        }

        var stateEntry = new ReviewState
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ItemId = item.Id,
            Stability = ReviewDefaults.InitialStability,
            DueAtUtc = DateTimeOffset.UtcNow
        };
        dbContext.ReviewStates.Add(stateEntry);
        state.ReviewStates.Add(stateEntry);

        return (item, true);
    }

    private static IReadOnlyList<DictionaryItem> GetVisibleItems(List<DictionaryItem> dictionaryItems, Guid userId)
    {
        return [.. dictionaryItems
            .Where(item => item.OwnerId is null || item.OwnerId == userId)
            .GroupBy(item => TextNormalizer.NormalizeForComparison(item.EnglishText))
            .Select(group => group
                .OrderByDescending(item => item.OwnerId == userId)
                .ThenBy(item => item.CreatedAtUtc)
                .First())];
    }

    private static async Task<AppState> LoadTrackedStateAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return new AppState
        {
            DictionaryItems = await dbContext.DictionaryItems.ToListAsync(cancellationToken),
            ExampleSentences = await dbContext.ExampleSentences.ToListAsync(cancellationToken),
            ReviewStates = await dbContext.ReviewStates.ToListAsync(cancellationToken),
            StudyEvents = await dbContext.StudyEvents.ToListAsync(cancellationToken),
            Imports = await dbContext.ImportRecords.ToListAsync(cancellationToken),
            ContentFlags = await dbContext.ContentFlags.ToListAsync(cancellationToken)
        };
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

    private static void MergeIntoCustom(
        DictionaryItem existing,
        string cleanedEnglish,
        List<string> cleanedGlosses,
        List<string> cleanedTags,
        string cleanedNotes,
        ItemKind itemKind,
        List<string> acceptedVariants,
        string normalizedEnglish)
    {
        existing.RussianGlosses = [.. existing.RussianGlosses
            .Concat(cleanedGlosses)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TextNormalizer.CleanInput)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        existing.Tags = [.. existing.Tags
            .Concat(cleanedTags)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TextNormalizer.CleanInput)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        existing.AcceptedVariants = [.. existing.AcceptedVariants
            .Concat(acceptedVariants)
            .Append(cleanedEnglish)
            .Append(normalizedEnglish)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TextNormalizer.CleanInput)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        existing.ItemKind = existing.ItemKind == ItemKind.Phrase || itemKind == ItemKind.Phrase
            ? ItemKind.Phrase
            : ItemKind.Word;

        if (!string.IsNullOrWhiteSpace(cleanedNotes) &&
            !existing.Notes.Contains(cleanedNotes, StringComparison.OrdinalIgnoreCase))
        {
            existing.Notes = string.IsNullOrWhiteSpace(existing.Notes)
                ? cleanedNotes
                : $"{existing.Notes}; {cleanedNotes}";
        }
    }

    private static ItemKind ResolveItemKind(string? rawItemKind, string englishText)
    {
        var cleanedKind = TextNormalizer.CleanInput(rawItemKind ?? "");

        if (string.Equals(cleanedKind, "phrase", StringComparison.OrdinalIgnoreCase))
        {
            return ItemKind.Phrase;
        }

        if (string.Equals(cleanedKind, "word", StringComparison.OrdinalIgnoreCase))
        {
            return ItemKind.Word;
        }

        return englishText.Contains(' ') ? ItemKind.Phrase : ItemKind.Word;
    }

    private static bool NormalizeCustomDuplicates(AppDbContext dbContext, AppState state, Guid userId)
    {
        var duplicates = state.DictionaryItems
            .Where(item => item.OwnerId == userId)
            .GroupBy(item => new
            {
                item.OwnerId,
                English = TextNormalizer.NormalizeForComparison(item.EnglishText)
            })
            .Where(group => group.Count() > 1)
            .ToList();

        if (duplicates.Count == 0)
        {
            return false;
        }

        foreach (var group in duplicates)
        {
            var primary = group.OrderBy(item => item.CreatedAtUtc).First();

            foreach (var duplicate in group.Where(item => item.Id != primary.Id))
            {
                MergeItemInto(dbContext, state, primary, duplicate, userId);
            }
        }

        return true;
    }

    private static void MergeItemInto(
        AppDbContext dbContext,
        AppState state,
        DictionaryItem primary,
        DictionaryItem duplicate,
        Guid userId)
    {
        primary.RussianGlosses = [.. primary.RussianGlosses
            .Concat(duplicate.RussianGlosses)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TextNormalizer.CleanInput)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        primary.Tags = [.. primary.Tags
            .Concat(duplicate.Tags)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TextNormalizer.CleanInput)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        primary.AcceptedVariants = [.. primary.AcceptedVariants
            .Concat(duplicate.AcceptedVariants)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TextNormalizer.CleanInput)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        primary.Distractors = [.. primary.Distractors
            .Concat(duplicate.Distractors)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        primary.ItemKind = primary.ItemKind == ItemKind.Phrase || duplicate.ItemKind == ItemKind.Phrase
            ? ItemKind.Phrase
            : ItemKind.Word;

        if (string.IsNullOrWhiteSpace(primary.Notes))
        {
            primary.Notes = duplicate.Notes;
        }
        else if (!string.IsNullOrWhiteSpace(duplicate.Notes) &&
                 !primary.Notes.Contains(duplicate.Notes, StringComparison.OrdinalIgnoreCase))
        {
            primary.Notes = $"{primary.Notes}; {duplicate.Notes}";
        }

        foreach (var sentence in state.ExampleSentences.Where(sentence => sentence.ItemId == duplicate.Id).ToList())
        {
            sentence.ItemId = primary.Id;
        }

        foreach (var reviewState in state.ReviewStates.Where(reviewState =>
                     reviewState.UserId == userId &&
                     reviewState.ItemId == duplicate.Id).ToList())
        {
            var existingState = state.ReviewStates.FirstOrDefault(candidate =>
                candidate.UserId == userId &&
                candidate.ItemId == primary.Id);

            if (existingState is null)
            {
                reviewState.ItemId = primary.Id;
                continue;
            }

            existingState.Stability = Math.Max(existingState.Stability, reviewState.Stability);
            existingState.DueAtUtc = existingState.DueAtUtc <= reviewState.DueAtUtc
                ? existingState.DueAtUtc
                : reviewState.DueAtUtc;
            existingState.LapseCount += reviewState.LapseCount;
            existingState.SuccessCount += reviewState.SuccessCount;
            existingState.LastSeenAtUtc = new[] { existingState.LastSeenAtUtc, reviewState.LastSeenAtUtc }
                .Where(value => value.HasValue)
                .OrderByDescending(value => value)
                .FirstOrDefault();
            dbContext.ReviewStates.Remove(reviewState);
            state.ReviewStates.Remove(reviewState);
        }

        foreach (var studyEvent in state.StudyEvents.Where(ev =>
                     ev.UserId == userId &&
                     ev.ItemId == duplicate.Id).ToList())
        {
            studyEvent.ItemId = primary.Id;
        }

        foreach (var flag in state.ContentFlags.Where(flag => flag.ItemId == duplicate.Id).ToList())
        {
            flag.ItemId = primary.Id;
        }

        dbContext.DictionaryItems.Remove(duplicate);
        state.DictionaryItems.Remove(duplicate);
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

    private static List<string> CleanValues(IEnumerable<string> values)
    {
        return [.. values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TextNormalizer.CleanInput)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static List<string> SplitPipeValues(string value)
    {
        return [.. value
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(TextNormalizer.CleanInput)
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))];
    }

    private sealed class AppState
    {
        public List<DictionaryItem> DictionaryItems { get; init; } = [];
        public List<ExampleSentence> ExampleSentences { get; init; } = [];
        public List<ReviewState> ReviewStates { get; init; } = [];
        public List<StudyEvent> StudyEvents { get; init; } = [];
        public List<ImportRecord> Imports { get; init; } = [];
        public List<ContentFlag> ContentFlags { get; init; } = [];
    }
}
