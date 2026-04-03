using Langoose.Domain.Enums;
using Langoose.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Langoose.Data.Seeding;

public sealed class DatabaseSeeder(AppDbContext dbContext)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var dictionaryItems = await dbContext.DictionaryItems.ToListAsync(cancellationToken);
        var exampleSentences = await dbContext.ExampleSentences.ToListAsync(cancellationToken);

        var seedItems = SeedDataLoader.LoadBaseItems();
        var changed = false;

        if (dictionaryItems.Count == 0)
        {
            foreach (var (item, sentence) in seedItems)
            {
                dbContext.DictionaryItems.Add(item);
                dbContext.ExampleSentences.Add(sentence);
            }

            changed = true;
        }
        else
        {
            changed = RepairBaseContent(dbContext, dictionaryItems, exampleSentences, seedItems);
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool RepairBaseContent(
        AppDbContext dbContext,
        List<DictionaryItem> dictionaryItems,
        List<ExampleSentence> exampleSentences,
        IReadOnlyList<(DictionaryItem Item, ExampleSentence Sentence)> seedItems)
    {
        var changed = false;

        foreach (var (seedItem, seedSentence) in seedItems)
        {
            var existingItem = dictionaryItems.FirstOrDefault(item =>
                item.SourceType == SourceType.Base &&
                string.Equals(item.EnglishText, seedItem.EnglishText, StringComparison.OrdinalIgnoreCase));

            if (existingItem is null)
            {
                dbContext.DictionaryItems.Add(seedItem);
                dbContext.ExampleSentences.Add(seedSentence);
                dictionaryItems.Add(seedItem);
                exampleSentences.Add(seedSentence);
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

            var existingSentence = exampleSentences.FirstOrDefault(
                sentence => sentence.ItemId == existingItem.Id);

            if (existingSentence is null)
            {
                var sentence = new ExampleSentence
                {
                    Id = Guid.NewGuid(),
                    ItemId = existingItem.Id,
                    SentenceText = seedSentence.SentenceText,
                    ClozeText = seedSentence.ClozeText,
                    TranslationHint = seedSentence.TranslationHint,
                    Origin = seedSentence.Origin,
                    QualityScore = seedSentence.QualityScore
                };
                dbContext.ExampleSentences.Add(sentence);
                exampleSentences.Add(sentence);
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
