using Langoose.Api.Infrastructure;
using Langoose.Api.Models;
using System.Text;

namespace Langoose.Api.Services;

public sealed class DictionaryService(IDataStore dataStore, EnrichmentService enrichmentService)
{
    private static readonly string[] RequiredCsvHeaders =
        ["english term", "russian translation s", "type"];

    private static readonly string[] OptionalCsvHeaders = ["notes", "tags"];

    public async Task<IReadOnlyList<DictionaryItem>> GetItemsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var store = await dataStore.LoadAsync(cancellationToken);

        if (NormalizeCustomDuplicates(store, userId))
        {
            await dataStore.SaveAsync(store, cancellationToken);
        }

        return GetVisibleItems(store, userId)
            .OrderBy(item => item.SourceType)
            .ThenBy(item => item.EnglishText)
            .ToList();
    }

    public async Task<DictionaryItem> AddItemAsync(
        Guid userId,
        DictionaryItemRequest request,
        CancellationToken cancellationToken)
    {
        var (item, _) = await UpsertItemAsync(userId, request, cancellationToken);

        return item;
    }

    public async Task<DictionaryItem?> PatchItemAsync(
        Guid userId,
        Guid itemId,
        DictionaryItemPatchRequest request,
        CancellationToken cancellationToken)
    {
        var store = await dataStore.LoadAsync(cancellationToken);
        var item = store.DictionaryItems.FirstOrDefault(candidate =>
            candidate.Id == itemId &&
            candidate.OwnerId == userId);

        if (item is null)
        {
            return null;
        }

        if (request.RussianGlosses is not null)
        {
            item.RussianGlosses = CleanValues(request.RussianGlosses);
        }

        if (!string.IsNullOrWhiteSpace(request.PartOfSpeech))
        {
            item.PartOfSpeech = TextNormalizer.CleanInput(request.PartOfSpeech);
        }

        if (!string.IsNullOrWhiteSpace(request.Difficulty))
        {
            item.Difficulty = TextNormalizer.CleanInput(request.Difficulty);
        }

        if (request.Notes is not null)
        {
            item.Notes = TextNormalizer.CleanInput(request.Notes);
        }

        if (request.Tags is not null)
        {
            item.Tags = CleanValues(request.Tags);
        }

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<DictionaryItemStatus>(request.Status, true, out var status))
        {
            item.Status = status;
        }

        await dataStore.SaveAsync(store, cancellationToken);

        return item;
    }

    public async Task<ImportCsvResponse> ImportCsvAsync(
        Guid userId,
        ImportCsvRequest request,
        CancellationToken cancellationToken)
    {
        var rows = request.CsvContent.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

        if (rows.Length == 0)
        {
            throw new ArgumentException("CSV file is empty.");
        }

        ValidateCsvHeader(rows[0]);

        var errors = new List<string>();
        var candidates = new List<DictionaryItemRequest>();

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
                : string.Empty;
            var tags = columns.Count > 4 ? SplitPipeValues(columns[4]) : [];

            if (string.IsNullOrWhiteSpace(english) || russianGlosses.Count == 0)
            {
                errors.Add($"Missing required fields: {row}");
                continue;
            }

            candidates.Add(new DictionaryItemRequest(
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
            return new ImportCsvResponse(
                Math.Max(0, rows.Length - 1),
                0,
                Math.Max(0, rows.Length - 1),
                errors);
        }

        var imported = 0;
        var skipped = 0;

        foreach (var candidate in candidates)
        {
            var (_, created) = await UpsertItemAsync(userId, candidate, cancellationToken);

            if (created)
            {
                imported++;
            }
            else
            {
                skipped++;
            }
        }

        var store = await dataStore.LoadAsync(cancellationToken);
        store.Imports.Add(new ImportRecord
        {
            UserId = userId,
            FileName = request.FileName,
            TotalRows = Math.Max(0, rows.Length - 1),
            ImportedRows = imported,
            SkippedRows = skipped
        });
        await dataStore.SaveAsync(store, cancellationToken);

        return new ImportCsvResponse(Math.Max(0, rows.Length - 1), imported, skipped, []);
    }

    public async Task<string> ExportCsvAsync(Guid userId, CancellationToken cancellationToken)
    {
        var store = await dataStore.LoadAsync(cancellationToken);

        if (NormalizeCustomDuplicates(store, userId))
        {
            await dataStore.SaveAsync(store, cancellationToken);
        }

        var rows = new List<string> { "English term,Russian translation(s),Type,Notes,Tags" };
        var visibleCustomItems = GetVisibleItems(store, userId)
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
        var store = await dataStore.LoadAsync(cancellationToken);
        var customItemIds = store.DictionaryItems
            .Where(item => item.OwnerId == userId)
            .Select(item => item.Id)
            .ToHashSet();

        store.DictionaryItems.RemoveAll(item => item.OwnerId == userId);
        store.ExampleSentences.RemoveAll(sentence => customItemIds.Contains(sentence.ItemId));
        store.ReviewStates.RemoveAll(state => state.UserId == userId);
        store.StudyEvents.RemoveAll(ev => ev.UserId == userId);
        store.Imports.RemoveAll(importRecord => importRecord.UserId == userId);
        store.ContentFlags.RemoveAll(flag =>
            flag.UserId == userId ||
            customItemIds.Contains(flag.ItemId));

        await dataStore.SaveAsync(store, cancellationToken);
    }

    private async Task<(DictionaryItem Item, bool Created)> UpsertItemAsync(
        Guid userId,
        DictionaryItemRequest request,
        CancellationToken cancellationToken)
    {
        var store = await dataStore.LoadAsync(cancellationToken);
        NormalizeCustomDuplicates(store, userId);

        var cleanedEnglish = TextNormalizer.CleanInput(request.EnglishText);
        var normalizedEnglish = TextNormalizer.NormalizeForComparison(cleanedEnglish);
        var itemKind = ResolveItemKind(request.ItemKind, cleanedEnglish);
        var existingCustom = store.DictionaryItems.FirstOrDefault(item =>
            item.OwnerId == userId &&
            TextNormalizer.NormalizeForComparison(item.EnglishText) == normalizedEnglish);
        var existingBase = store.DictionaryItems.FirstOrDefault(item =>
            item.OwnerId is null &&
            TextNormalizer.NormalizeForComparison(item.EnglishText) == normalizedEnglish);

        var enrichment = enrichmentService.Enrich(
            new EnrichmentRequest(cleanedEnglish, request.RussianGlosses, request.ItemKind));
        var glosses = (request.RussianGlosses?.Count ?? 0) > 0
            ? request.RussianGlosses!
            : enrichment.RussianGlosses;
        var cleanedGlosses = CleanValues(glosses);
        var cleanedTags = CleanValues(request.Tags ?? []);
        var cleanedNotes = TextNormalizer.CleanInput(request.Notes ?? string.Empty);

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
            await dataStore.SaveAsync(store, cancellationToken);

            return (existingCustom, false);
        }

        if (existingBase is not null)
        {
            var reviewState = store.ReviewStates.FirstOrDefault(state =>
                state.UserId == userId &&
                state.ItemId == existingBase.Id);

            if (reviewState is null)
            {
                store.ReviewStates.Add(new ReviewState
                {
                    UserId = userId,
                    ItemId = existingBase.Id,
                    DueAtUtc = DateTimeOffset.UtcNow
                });
                await dataStore.SaveAsync(store, cancellationToken);
            }

            return (existingBase, false);
        }

        var item = new DictionaryItem
        {
            OwnerId = userId,
            SourceType = SourceType.Custom,
            EnglishText = cleanedEnglish,
            RussianGlosses = cleanedGlosses.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ItemKind = itemKind,
            PartOfSpeech = request.PartOfSpeech?.Trim() ?? enrichment.PartOfSpeech,
            Difficulty = request.Difficulty?.Trim() ?? enrichment.Difficulty,
            Notes = cleanedNotes,
            Tags = cleanedTags,
            CreatedByFlow = request.CreatedByFlow?.Trim() ?? "quick-add",
            AcceptedVariants = enrichment.AcceptedVariants
                .Append(cleanedEnglish)
                .Append(normalizedEnglish)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(TextNormalizer.CleanInput)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        item.Distractors = item.ItemKind == ItemKind.Phrase
            ? ["make up", "look after", "find out"]
            : ["get", "make", "take"];

        store.DictionaryItems.Add(item);

        if (request.GenerateExamples)
        {
            var validExamples = enrichment.Examples.Where(candidate =>
                !enrichment.ValidationWarnings.Contains(
                    "Example sentence must include the target term."));

            foreach (var example in validExamples)
            {
                store.ExampleSentences.Add(new ExampleSentence
                {
                    ItemId = item.Id,
                    SentenceText = example.SentenceText,
                    ClozeText = example.ClozeText,
                    TranslationHint = example.TranslationHint,
                    QualityScore = example.QualityScore,
                    Origin = ContentOrigin.Manual
                });
            }
        }

        store.ReviewStates.Add(new ReviewState
        {
            UserId = userId,
            ItemId = item.Id,
            DueAtUtc = DateTimeOffset.UtcNow
        });

        await dataStore.SaveAsync(store, cancellationToken);

        return (item, true);
    }

    private static IReadOnlyList<DictionaryItem> GetVisibleItems(DataStore store, Guid userId)
    {
        return store.DictionaryItems
            .Where(item => item.OwnerId is null || item.OwnerId == userId)
            .GroupBy(item => TextNormalizer.NormalizeForComparison(item.EnglishText))
            .Select(group => group
                .OrderByDescending(item => item.OwnerId == userId)
                .ThenBy(item => item.CreatedAtUtc)
                .First())
            .ToList();
    }

    private static void ValidateCsvHeader(string headerRow)
    {
        var headers = ParseCsvRow(headerRow)
            .Select(header => TextNormalizer.NormalizeForComparison(header))
            .ToList();

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
        existing.RussianGlosses = existing.RussianGlosses
            .Concat(cleanedGlosses)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TextNormalizer.CleanInput)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        existing.Tags = existing.Tags
            .Concat(cleanedTags)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TextNormalizer.CleanInput)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        existing.AcceptedVariants = existing.AcceptedVariants
            .Concat(acceptedVariants)
            .Append(cleanedEnglish)
            .Append(normalizedEnglish)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TextNormalizer.CleanInput)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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
        var cleanedKind = TextNormalizer.CleanInput(rawItemKind ?? string.Empty);

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

    private static bool NormalizeCustomDuplicates(DataStore store, Guid userId)
    {
        var duplicates = store.DictionaryItems
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
                MergeItemInto(store, primary, duplicate, userId);
            }
        }

        return true;
    }

    private static void MergeItemInto(
        DataStore store,
        DictionaryItem primary,
        DictionaryItem duplicate,
        Guid userId)
    {
        primary.RussianGlosses = primary.RussianGlosses
            .Concat(duplicate.RussianGlosses)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TextNormalizer.CleanInput)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        primary.Tags = primary.Tags
            .Concat(duplicate.Tags)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TextNormalizer.CleanInput)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        primary.AcceptedVariants = primary.AcceptedVariants
            .Concat(duplicate.AcceptedVariants)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TextNormalizer.CleanInput)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        primary.Distractors = primary.Distractors
            .Concat(duplicate.Distractors)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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

        foreach (var sentence in store.ExampleSentences.Where(sentence => sentence.ItemId == duplicate.Id).ToList())
        {
            sentence.ItemId = primary.Id;
        }

        foreach (var state in store.ReviewStates.Where(state =>
                     state.UserId == userId &&
                     state.ItemId == duplicate.Id).ToList())
        {
            var existingState = store.ReviewStates.FirstOrDefault(candidate =>
                candidate.UserId == userId &&
                candidate.ItemId == primary.Id);

            if (existingState is null)
            {
                state.ItemId = primary.Id;
                continue;
            }

            existingState.Stability = Math.Max(existingState.Stability, state.Stability);
            existingState.DueAtUtc = existingState.DueAtUtc <= state.DueAtUtc
                ? existingState.DueAtUtc
                : state.DueAtUtc;
            existingState.LapseCount += state.LapseCount;
            existingState.SuccessCount += state.SuccessCount;
            existingState.LastSeenAtUtc = new[] { existingState.LastSeenAtUtc, state.LastSeenAtUtc }
                .Where(value => value.HasValue)
                .OrderByDescending(value => value)
                .FirstOrDefault();
            store.ReviewStates.Remove(state);
        }

        foreach (var studyEvent in store.StudyEvents.Where(ev =>
                     ev.UserId == userId &&
                     ev.ItemId == duplicate.Id).ToList())
        {
            studyEvent.ItemId = primary.Id;
        }

        foreach (var flag in store.ContentFlags.Where(flag => flag.ItemId == duplicate.Id).ToList())
        {
            flag.ItemId = primary.Id;
        }

        store.DictionaryItems.Remove(duplicate);
    }

    private static List<string> ParseCsvRow(string row)
    {
        var result = new List<string>();
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
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(TextNormalizer.CleanInput)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> SplitPipeValues(string value)
    {
        return value
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(TextNormalizer.CleanInput)
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .ToList();
    }
}
