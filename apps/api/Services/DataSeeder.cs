using Langoose.Api.Infrastructure;
using Langoose.Api.Models;

namespace Langoose.Api.Services;

public sealed class DataSeeder(IDataStore dataStore)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var store = await dataStore.LoadAsync(cancellationToken);
        var changed = false;

        if (store.DictionaryItems.Count == 0)
        {
            foreach (var (item, sentence) in SeedData.BaseItems)
            {
                store.DictionaryItems.Add(item);
                store.ExampleSentences.Add(sentence);
            }

            changed = true;
        }
        else
        {
            changed = RepairBaseContent(store);
        }

        if (changed)
        {
            await dataStore.SaveAsync(store, cancellationToken);
        }
    }

    private static bool RepairBaseContent(DataStore store)
    {
        var changed = false;

        foreach (var (seedItem, seedSentence) in SeedData.BaseItems)
        {
            var existingItem = store.DictionaryItems.FirstOrDefault(item =>
                item.SourceType == SourceType.Base &&
                string.Equals(
                    item.EnglishText,
                    seedItem.EnglishText,
                    StringComparison.OrdinalIgnoreCase));

            if (existingItem is null)
            {
                store.DictionaryItems.Add(seedItem);
                store.ExampleSentences.Add(seedSentence);
                changed = true;
                continue;
            }

            if (!existingItem.RussianGlosses.SequenceEqual(seedItem.RussianGlosses) ||
                existingItem.ItemKind != seedItem.ItemKind ||
                existingItem.PartOfSpeech != seedItem.PartOfSpeech ||
                existingItem.Difficulty != seedItem.Difficulty)
            {
                existingItem.RussianGlosses = [.. seedItem.RussianGlosses];
                existingItem.ItemKind = seedItem.ItemKind;
                existingItem.PartOfSpeech = seedItem.PartOfSpeech;
                existingItem.Difficulty = seedItem.Difficulty;
                changed = true;
            }

            var existingSentence = store.ExampleSentences.FirstOrDefault(
                sentence => sentence.ItemId == existingItem.Id);

            if (existingSentence is null)
            {
                store.ExampleSentences.Add(new ExampleSentence
                {
                    ItemId = existingItem.Id,
                    SentenceText = seedSentence.SentenceText,
                    ClozeText = seedSentence.ClozeText,
                    TranslationHint = seedSentence.TranslationHint,
                    Origin = seedSentence.Origin,
                    QualityScore = seedSentence.QualityScore
                });
                changed = true;
                continue;
            }

            if (existingSentence.ClozeText != seedSentence.ClozeText ||
                existingSentence.TranslationHint != seedSentence.TranslationHint)
            {
                existingSentence.SentenceText = seedSentence.SentenceText;
                existingSentence.ClozeText = seedSentence.ClozeText;
                existingSentence.TranslationHint = seedSentence.TranslationHint;
                existingSentence.Origin = seedSentence.Origin;
                existingSentence.QualityScore = seedSentence.QualityScore;
                changed = true;
            }
        }

        return changed;
    }
}
