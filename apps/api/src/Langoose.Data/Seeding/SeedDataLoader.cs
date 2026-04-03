using System.Text.Json;
using Langoose.Domain.Models;

namespace Langoose.Data.Seeding;

public static class SeedDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<(DictionaryItem Item, ExampleSentence Sentence)> LoadBaseItems()
    {
        using var stream = OpenSeedStream();
        var payload = JsonSerializer.Deserialize<SeedPayload>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Seed payload could not be deserialized.");

        return payload.Items.Select(CreatePair).ToArray();
    }

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

    private static (DictionaryItem Item, ExampleSentence Sentence) CreatePair(SeedItem seedItem)
    {
        var itemId = Guid.NewGuid();
        var item = new DictionaryItem
        {
            Id = itemId,
            SourceType = SourceType.Base,
            EnglishText = seedItem.English,
            RussianGlosses = [.. seedItem.Glosses],
            ItemKind = Enum.Parse<ItemKind>(seedItem.Kind, ignoreCase: true),
            PartOfSpeech = seedItem.PartOfSpeech,
            Difficulty = seedItem.Difficulty,
            CreatedByFlow = "seed",
            AcceptedVariants = seedItem.AcceptedVariants.Count == 0
                ? [seedItem.English]
                : [seedItem.English, .. seedItem.AcceptedVariants],
            Distractors = ["make", "get", "use"]
        };

        var sentence = new ExampleSentence
        {
            ItemId = itemId,
            SentenceText = seedItem.Cloze.Replace("____", seedItem.English, StringComparison.Ordinal),
            ClozeText = seedItem.Cloze,
            TranslationHint = seedItem.TranslationHint,
            Origin = ContentOrigin.Dataset,
            QualityScore = 0.95
        };

        return (item, sentence);
    }

    private sealed record SeedPayload(List<SeedItem> Items);

    private sealed record SeedItem(
        string English,
        List<string> Glosses,
        string Kind,
        string PartOfSpeech,
        string Difficulty,
        string Cloze,
        string TranslationHint,
        List<string> AcceptedVariants);
}
