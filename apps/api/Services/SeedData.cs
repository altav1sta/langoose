using Langoose.Api.Models;

namespace Langoose.Api.Services;

public static class SeedData
{
    public static IReadOnlyList<(DictionaryItem Item, ExampleSentence Sentence)> BaseItems =>
    [
        Create("book", ["книга"], ItemKind.Word, "noun", "A1", "I read a ____ before bed.", "Я читаю книгу перед сном."),
        Create("go", ["идти", "ехать"], ItemKind.Word, "verb", "A1", "We usually ____ to school by bus.", "Мы обычно ездим в школу на автобусе.", ["travel"]),
        Create("take care of", ["заботиться о"], ItemKind.Phrase, "phrase", "B1", "They ____ their grandmother every weekend.", "Они заботятся о своей бабушке каждые выходные.", ["look after"]),
        Create("decision", ["решение"], ItemKind.Word, "noun", "B1", "It was a difficult ____ to make.", "Это было трудное решение.", ["choice"]),
        Create("at least", ["по крайней мере"], ItemKind.Phrase, "phrase", "A2", "Call me once a week, ____.", "Позвони мне хотя бы раз в неделю.")
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
