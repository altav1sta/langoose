using Langoose.Api.Models;

namespace Langoose.Api.Services;

public static class SeedData
{
    public static IReadOnlyList<(DictionaryItem Item, ExampleSentence Sentence)> BaseItems =>
    [
        Create(
            "book",
            ["\u043A\u043D\u0438\u0433\u0430"],
            ItemKind.Word,
            "noun",
            "A1",
            "I read a ____ before bed.",
            "\u042F \u0447\u0438\u0442\u0430\u044E \u043A\u043D\u0438\u0433\u0443 " +
                "\u043F\u0435\u0440\u0435\u0434 \u0441\u043D\u043E\u043C."),
        Create(
            "go",
            ["\u0438\u0434\u0442\u0438", "\u0435\u0445\u0430\u0442\u044C"],
            ItemKind.Word,
            "verb",
            "A1",
            "We usually ____ to school by bus.",
            "\u041C\u044B \u043E\u0431\u044B\u0447\u043D\u043E \u0435\u0437\u0434\u0438\u043C " +
                "\u0432 \u0448\u043A\u043E\u043B\u0443 \u043D\u0430 \u0430\u0432\u0442\u043E\u0431\u0443\u0441\u0435.",
            ["travel"]),
        Create(
            "take care of",
            ["\u0437\u0430\u0431\u043E\u0442\u0438\u0442\u044C\u0441\u044F \u043E"],
            ItemKind.Phrase,
            "phrase",
            "B1",
            "They ____ their grandmother every weekend.",
            "\u041E\u043D\u0438 \u0437\u0430\u0431\u043E\u0442\u044F\u0442\u0441\u044F " +
                "\u043E \u0441\u0432\u043E\u0435\u0439 \u0431\u0430\u0431\u0443\u0448\u043A\u0435 " +
                "\u043A\u0430\u0436\u0434\u044B\u0435 \u0432\u044B\u0445\u043E\u0434\u043D\u044B\u0435.",
            ["look after"]),
        Create(
            "decision",
            ["\u0440\u0435\u0448\u0435\u043D\u0438\u0435"],
            ItemKind.Word,
            "noun",
            "B1",
            "It was a difficult ____ to make.",
            "\u042D\u0442\u043E \u0431\u044B\u043B\u043E \u0442\u0440\u0443\u0434\u043D\u043E\u0435 " +
                "\u0440\u0435\u0448\u0435\u043D\u0438\u0435.",
            ["choice"]),
        Create(
            "at least",
            ["\u043F\u043E \u043A\u0440\u0430\u0439\u043D\u0435\u0439 \u043C\u0435\u0440\u0435"],
            ItemKind.Phrase,
            "phrase",
            "A2",
            "Call me once a week, ____.",
            "\u041F\u043E\u0437\u0432\u043E\u043D\u0438 \u043C\u043D\u0435 \u0445\u043E\u0442\u044F " +
                "\u0431\u044B \u0440\u0430\u0437 \u0432 \u043D\u0435\u0434\u0435\u043B\u044E.")
    ];

    private static (DictionaryItem, ExampleSentence) Create(
        string english,
        List<string> glosses,
        ItemKind kind,
        string pos,
        string difficulty,
        string cloze,
        string translationHint,
        List<string>? acceptedVariants = null)
    {
        var itemId = Guid.NewGuid();
        var item = new DictionaryItem
        {
            Id = itemId,
            SourceType = SourceType.Base,
            EnglishText = english,
            RussianGlosses = glosses,
            ItemKind = kind,
            PartOfSpeech = pos,
            Difficulty = difficulty,
            CreatedByFlow = "seed",
            AcceptedVariants = acceptedVariants is null ? [english] : [english, .. acceptedVariants],
            Distractors = ["make", "get", "use"]
        };

        var sentence = new ExampleSentence
        {
            ItemId = itemId,
            SentenceText = cloze.Replace("____", english),
            ClozeText = cloze,
            TranslationHint = translationHint,
            Origin = ContentOrigin.Dataset,
            QualityScore = 0.95
        };

        return (item, sentence);
    }
}
