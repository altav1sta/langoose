using Langoose.Domain.Abstractions;
using Langoose.Domain.Models;

namespace Langoose.Data.Seeding;

public sealed class DatabaseSeeder(IDataStore dataStore)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var store = await dataStore.LoadAsync(cancellationToken);
        var changed = false;
        var seedItems = SeedDataLoader.LoadBaseItems();

        if (store.DictionaryItems.Count == 0)
        {
            foreach (var (item, sentence) in seedItems)
            {
                store.DictionaryItems.Add(item);
                store.ExampleSentences.Add(sentence);
            }

            changed = true;
        }
        else
        {
            changed = RepairBaseContent(store, seedItems);
        }

        if (changed)
        {
            await dataStore.SaveAsync(store, cancellationToken);
        }
    }

    private static bool RepairBaseContent(
        DataStore store,
        IReadOnlyList<(DictionaryItem Item, ExampleSentence Sentence)> seedItems)
    {
        var changed = false;

        foreach (var (seedItem, seedSentence) in seedItems)
        {
            var existingItem = store.DictionaryItems.FirstOrDefault(item =>
                item.SourceType == SourceType.Base &&
                string.Equals(item.EnglishText, seedItem.EnglishText, StringComparison.OrdinalIgnoreCase));

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
                existingItem.Difficulty != seedItem.Difficulty ||
                !existingItem.AcceptedVariants.SequenceEqual(seedItem.AcceptedVariants) ||
                !existingItem.Distractors.SequenceEqual(seedItem.Distractors))
            {
                existingItem.RussianGlosses = [.. seedItem.RussianGlosses];
                existingItem.ItemKind = seedItem.ItemKind;
                existingItem.PartOfSpeech = seedItem.PartOfSpeech;
                existingItem.Difficulty = seedItem.Difficulty;
                existingItem.AcceptedVariants = [.. seedItem.AcceptedVariants];
                existingItem.Distractors = [.. seedItem.Distractors];
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

            if (existingSentence.SentenceText != seedSentence.SentenceText ||
                existingSentence.ClozeText != seedSentence.ClozeText ||
                existingSentence.TranslationHint != seedSentence.TranslationHint ||
                existingSentence.Origin != seedSentence.Origin ||
                existingSentence.QualityScore != seedSentence.QualityScore)
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
