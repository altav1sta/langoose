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
        var translations = new List<EntryTranslation>();
        var contexts = new List<EntryContext>();

        var now = DateTimeOffset.UtcNow;

        foreach (var item in payload.Items)
        {
            var enEntry = new DictionaryEntry
            {
                Id = Guid.CreateVersion7(),
                Language = "en",
                Text = item.English,
                IsBaseForm = true,
                GrammarLabel = item.GrammarLabel,
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
                IsBaseForm = true,
                GrammarLabel = item.GrammarLabel,
                Difficulty = item.Difficulty,
                IsPublic = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            entries.Add(enEntry);
            entries.Add(ruEntry);

            translations.Add(new EntryTranslation
            {
                SourceEntryId = enEntry.Id,
                TargetEntryId = ruEntry.Id,
                CreatedAtUtc = now
            });
            translations.Add(new EntryTranslation
            {
                SourceEntryId = ruEntry.Id,
                TargetEntryId = enEntry.Id,
                CreatedAtUtc = now
            });

            var sentenceText = item.Cloze.Replace("____", item.English, StringComparison.Ordinal);
            contexts.Add(new EntryContext
            {
                Id = Guid.CreateVersion7(),
                DictionaryEntryId = enEntry.Id,
                Text = sentenceText,
                Cloze = item.Cloze,
                Difficulty = item.Difficulty,
                CreatedAtUtc = now
            });

            contexts.Add(new EntryContext
            {
                Id = Guid.CreateVersion7(),
                DictionaryEntryId = ruEntry.Id,
                Text = item.TranslationHint,
                Cloze = item.TranslationHint,
                Difficulty = item.Difficulty,
                CreatedAtUtc = now
            });
        }

        return new SeedBatch(entries, translations, contexts);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static Stream OpenSeedStream()
    {
        var assembly = typeof(SeedDataLoader).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith("Seeding.Json.base-store.json", StringComparison.Ordinal));

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
        string GrammarLabel,
        string Cloze,
        string TranslationHint);
}

public sealed record SeedBatch(
    IReadOnlyList<DictionaryEntry> Entries,
    IReadOnlyList<EntryTranslation> Translations,
    IReadOnlyList<EntryContext> Contexts);
