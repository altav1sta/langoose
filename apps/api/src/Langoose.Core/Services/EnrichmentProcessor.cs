using Langoose.Data;
using Langoose.Domain.Enums;
using Langoose.Domain.Models;
using Langoose.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Langoose.Core.Services;

public sealed class EnrichmentProcessor(
    AppDbContext dbContext,
    IEnrichmentProvider enrichmentProvider,
    ILogger<EnrichmentProcessor> logger) : IEnrichmentProcessor
{
    public async Task ProcessPendingBatchAsync(
        int batchSize, int maxRetries, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var pendingItems = await dbContext.UserDictionaryEntries
            .Include(x => x.SourceEntry).ThenInclude(x => x!.Senses)
                .ThenInclude(x => x.Translations).ThenInclude(x => x.TargetSense)
                .ThenInclude(x => x.DictionaryEntry)
            .Include(x => x.TargetEntry).ThenInclude(x => x!.Senses)
            .Where(x => x.EnrichmentStatus == EnrichmentStatus.Pending
                || (x.EnrichmentStatus == EnrichmentStatus.ProviderError && x.EnrichmentAttempts < maxRetries))
            .Where(x => x.EnrichmentNotBefore == null || x.EnrichmentNotBefore <= now)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);

        if (pendingItems.Length == 0)
            return;

        logger.LogInformation("Found {Count} pending items to enrich.", pendingItems.Length);

        // Step 1: load existing entries for items missing navigations.
        // Skip keys where Include already loaded the entry.
        var lookupKeys = new HashSet<EntryKey>();

        foreach (var item in pendingItems)
        {
            if (item.SourceEntry == null)
                lookupKeys.Add(new EntryKey(item.SourceLanguage, item.UserInputTerm, item.PartOfSpeech));

            if (item.TargetEntry == null && item.UserInputTranslation is not null)
                lookupKeys.Add(new EntryKey(item.TargetLanguage, item.UserInputTranslation, item.PartOfSpeech));
        }

        var baseEntryLookup = new Dictionary<EntryKey, DictionaryEntry>(0);

        if (lookupKeys.Count > 0)
        {
            var languages = lookupKeys.Select(x => x.Language).Distinct().ToArray();
            var texts = lookupKeys.Select(x => x.Text).Distinct().ToArray();

            var dbEntries = await dbContext.DictionaryEntries
                .Include(x => x.Senses)
                    .ThenInclude(x => x.Translations).ThenInclude(x => x.TargetSense)
                    .ThenInclude(x => x.DictionaryEntry)
                .Where(x => languages.Contains(x.Language) && texts.Contains(x.Text))
                .ToArrayAsync(cancellationToken);

            baseEntryLookup = dbEntries
                .Where(x => lookupKeys.Contains(new EntryKey(x.Language, x.Text, x.PartOfSpeech)))
                .ToDictionary(
                    x => new EntryKey(x.Language, x.Text, x.PartOfSpeech),
                    x => x.BaseEntryId is null
                        ? x
                        : dbEntries.FirstOrDefault(y => y.Id == x.BaseEntryId) ?? x);
        }

        // Step 2: resolve navigations from lookup, split into resolved vs needing enrichment
        var itemsToEnrich = new List<UserDictionaryEntry>();

        foreach (var item in pendingItems)
        {
            if (item.SourceEntry == null)
            {
                var sourceKey = new EntryKey(item.SourceLanguage, item.UserInputTerm, item.PartOfSpeech);
                item.SourceEntry = baseEntryLookup.GetValueOrDefault(sourceKey);
            }

            if (item.TargetEntry == null)
            {
                if (item.UserInputTranslation is not null)
                {
                    var targetKey = new EntryKey(item.TargetLanguage, item.UserInputTranslation, item.PartOfSpeech);
                    item.TargetEntry = baseEntryLookup.GetValueOrDefault(targetKey);
                }
                else
                {
                    item.TargetEntry = item.SourceEntry?.Senses
                        .SelectMany(s => s.Translations)
                        .Select(t => t.TargetSense.DictionaryEntry)
                        .FirstOrDefault(e => e.Language == item.TargetLanguage && e.PartOfSpeech == item.PartOfSpeech);
                }
            }

            var hasLink = item.SourceEntry is not null && item.TargetEntry is not null
                && item.SourceEntry.Senses
                    .SelectMany(s => s.Translations)
                    .Any(t => t.TargetSense.DictionaryEntryId == item.TargetEntry.Id);

            if (item.SourceEntry is not null && item.TargetEntry is not null && hasLink)
            {
                item.EnrichmentStatus = EnrichmentStatus.Enriched;
                item.UpdatedAtUtc = now;
            }
            else
            {
                itemsToEnrich.Add(item);
            }
        }

        // Step 3: call provider for items needing work
        if (itemsToEnrich.Count > 0)
        {
            EnrichmentResult[] results;

            try
            {
                results = await enrichmentProvider.EnrichBatchAsync([.. itemsToEnrich], cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "EnrichBatchAsync failed for {Count} items.", itemsToEnrich.Count);

                foreach (var item in itemsToEnrich)
                    HandleItemFailure(item, maxRetries, now);

                await dbContext.SaveChangesAsync(cancellationToken);

                return;
            }

            var resultByItem = results.ToDictionary(x => x.UserEntryId);

            foreach (var item in itemsToEnrich)
            {
                if (!resultByItem.TryGetValue(item.Id, out var result))
                    continue;

                if (result.Status != EnrichmentStatus.Enriched)
                {
                    item.EnrichmentStatus = result.Status;
                    item.UpdatedAtUtc = now;
                    continue;
                }

                try
                {
                    var pos = item.PartOfSpeech;

                    if (item.SourceEntry == null)
                    {
                        var lang = item.SourceLanguage;
                        item.SourceEntry = CreateBaseEntryOrThrow(result.SourceEntries, lang, pos, now);
                        CreateDerivedEntries(result.SourceEntries, item.SourceEntry, lang, pos, now);
                    }

                    if (item.TargetEntry == null)
                    {
                        var lang = item.TargetLanguage;
                        item.TargetEntry = CreateBaseEntryOrThrow(result.TargetEntries, lang, pos, now);
                        CreateDerivedEntries(result.TargetEntries, item.TargetEntry, lang, pos, now);
                    }

                    LinkSenses(item.SourceEntry, item.TargetEntry, now);
                    item.EnrichmentStatus = EnrichmentStatus.Enriched;
                    item.UpdatedAtUtc = now;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to materialize result for item {Id}.", item.Id);
                    HandleItemFailure(item, maxRetries, now);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private DictionaryEntry CreateBaseEntryOrThrow(
        EnrichedEntry[]? enriched, string language, string partOfSpeech, DateTimeOffset now)
    {
        var baseSrc = enriched?.FirstOrDefault(x => x.IsBaseForm)
            ?? throw new InvalidOperationException($"Provider returned no base form for language '{language}'.");

        var entry = MakeEntry(baseSrc, language, partOfSpeech, null, now);
        dbContext.DictionaryEntries.Add(entry);

        var sense = new Sense
        {
            Id = Guid.CreateVersion7(),
            DictionaryEntryId = entry.Id,
            SenseIndex = 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        entry.Senses.Add(sense);
        dbContext.Senses.Add(sense);

        return entry;
    }

    private void CreateDerivedEntries(
        EnrichedEntry[]? enriched, DictionaryEntry baseEntry, string language, string partOfSpeech, DateTimeOffset now)
    {
        if (enriched == null)
            return;

        foreach (var src in enriched.Where(x => !x.IsBaseForm))
            dbContext.DictionaryEntries.Add(MakeEntry(src, language, partOfSpeech, baseEntry.Id, now));
    }

    private void LinkSenses(DictionaryEntry source, DictionaryEntry target, DateTimeOffset now)
    {
        var sourceSense = source.Senses.OrderBy(x => x.SenseIndex).FirstOrDefault()
            ?? throw new InvalidOperationException($"Source entry {source.Id} has no senses to link.");
        var targetSense = target.Senses.OrderBy(x => x.SenseIndex).FirstOrDefault()
            ?? throw new InvalidOperationException($"Target entry {target.Id} has no senses to link.");

        if (sourceSense.Translations.Any(x => x.TargetSenseId == targetSense.Id))
            return;

        dbContext.SenseTranslations.Add(new SenseTranslation
        {
            SourceSenseId = sourceSense.Id,
            TargetSenseId = targetSense.Id,
            Rank = 0,
            CreatedAtUtc = now
        });

        dbContext.SenseTranslations.Add(new SenseTranslation
        {
            SourceSenseId = targetSense.Id,
            TargetSenseId = sourceSense.Id,
            Rank = 0,
            CreatedAtUtc = now
        });
    }

    private static DictionaryEntry MakeEntry(
        EnrichedEntry src, string language, string partOfSpeech, Guid? baseEntryId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        Language = language,
        Text = src.Text,
        BaseEntryId = baseEntryId,
        PartOfSpeech = partOfSpeech,
        GrammarLabel = src.GrammarLabel,
        Difficulty = src.Difficulty,
        IsPublic = false,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };

    private void HandleItemFailure(UserDictionaryEntry item, int maxRetries, DateTimeOffset now)
    {
        item.EnrichmentAttempts++;
        item.UpdatedAtUtc = now;

        if (item.EnrichmentAttempts >= maxRetries)
        {
            item.EnrichmentStatus = EnrichmentStatus.ProviderError;
            logger.LogWarning("Item {Id} marked as ProviderError after {Attempts} attempts.",
                item.Id, item.EnrichmentAttempts);
        }
        else
        {
            var delaySeconds = Math.Pow(2, item.EnrichmentAttempts) * 10;
            item.EnrichmentNotBefore = now.AddSeconds(delaySeconds);
            logger.LogWarning("Item {Id} retry scheduled after {Delay}s (attempt {Attempt}/{Max}).",
                item.Id, delaySeconds, item.EnrichmentAttempts, maxRetries);
        }
    }
}
