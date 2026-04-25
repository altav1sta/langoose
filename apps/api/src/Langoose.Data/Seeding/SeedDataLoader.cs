using System.Text.Json;
using Langoose.Domain.Models;

namespace Langoose.Data.Seeding;

public static class SeedDataLoader
{
    public static SeedBatch LoadBaseItems()
    {
        using var stream = OpenSeedStream();
        var payload = JsonSerializer.Deserialize<SeedPayload>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Seed payload could not be deserialized.");

        var entries = new List<DictionaryEntry>();
        var senses = new List<Sense>();
        var senseTranslations = new List<SenseTranslation>();
        var contexts = new List<EntryContext>();

        var now = DateTimeOffset.UtcNow;

        foreach (var item in payload.Items)
        {
            var enEntry = new DictionaryEntry
            {
                Id = Guid.CreateVersion7(),
                Language = "en",
                Text = item.English,
                PartOfSpeech = item.PartOfSpeech,
                Difficulty = item.Difficulty,
                IsPublic = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            var ruEntry = new DictionaryEntry
            {
                Id = Guid.CreateVersion7(),
                Language = "ru",
                Text = item.Russian,
                PartOfSpeech = item.PartOfSpeech,
                Difficulty = item.Difficulty,
                IsPublic = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            entries.Add(enEntry);
            entries.Add(ruEntry);

            var enSense = new Sense
            {
                Id = Guid.CreateVersion7(),
                DictionaryEntryId = enEntry.Id,
                SenseIndex = 0,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            var ruSense = new Sense
            {
                Id = Guid.CreateVersion7(),
                DictionaryEntryId = ruEntry.Id,
                SenseIndex = 0,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            senses.Add(enSense);
            senses.Add(ruSense);

            senseTranslations.Add(new SenseTranslation
            {
                SourceSenseId = enSense.Id,
                TargetSenseId = ruSense.Id,
                Rank = 0,
                CreatedAtUtc = now
            });

            senseTranslations.Add(new SenseTranslation
            {
                SourceSenseId = ruSense.Id,
                TargetSenseId = enSense.Id,
                Rank = 0,
                CreatedAtUtc = now
            });

            var sentenceText = item.Cloze.Replace("____", item.English, StringComparison.Ordinal);
            var enContext = new EntryContext
            {
                Id = Guid.CreateVersion7(),
                DictionaryEntryId = enEntry.Id,
                Text = sentenceText,
                Cloze = item.Cloze,
                Difficulty = item.Difficulty,
                CreatedAtUtc = now
            };

            var ruContext = new EntryContext
            {
                Id = Guid.CreateVersion7(),
                DictionaryEntryId = ruEntry.Id,
                Text = item.TranslationHint,
                Cloze = item.TranslationHint,
                Difficulty = item.Difficulty,
                CreatedAtUtc = now
            };

            enContext.Translations.Add(ruContext);
            ruContext.Translations.Add(enContext);

            contexts.Add(enContext);
            contexts.Add(ruContext);
        }

        return new SeedBatch(entries, senses, senseTranslations, contexts);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static Stream OpenSeedStream()
    {
        var assembly = typeof(SeedDataLoader).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(x => x.EndsWith("Seeding.Json.base-store.json", StringComparison.Ordinal));

        if (resourceName is null)
        {
            throw new InvalidOperationException("Seed resource 'Seeding/Json/base-store.json' was not found.");
        }

        return assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Seed resource stream could not be opened.");
    }

    private sealed record SeedPayload(List<SeedItem> Items);

    private sealed record SeedItem(
        string English,
        string Russian,
        string Difficulty,
        string PartOfSpeech,
        string Cloze,
        string TranslationHint);
}

public sealed record SeedBatch(
    IReadOnlyList<DictionaryEntry> Entries,
    IReadOnlyList<Sense> Senses,
    IReadOnlyList<SenseTranslation> SenseTranslations,
    IReadOnlyList<EntryContext> Contexts);
