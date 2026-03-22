using Langoose.Api.Models;

namespace Langoose.Api.Services;

public sealed class EnrichmentService
{
    private static readonly Dictionary<string, (List<string> Glosses, string Pos, string Difficulty, List<string> Examples, List<string> Variants)> Lexicon =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["book"] = (["книга"], "noun", "A1", ["She bought a new book yesterday."], []),
            ["decision"] = (["решение"], "noun", "B1", ["Making that decision took a long time."], ["choice"]),
            ["take care of"] = (["заботиться о"], "phrase", "B1", ["I take care of my little brother after school."], ["look after"]),
            ["look for"] = (["искать"], "phrase", "A2", ["We are looking for a small apartment."], ["search for"]),
            ["improve"] = (["улучшать"], "verb", "B1", ["Practice every day to improve your English."], ["get better"])
        };

    public EnrichmentResponse Enrich(EnrichmentRequest request)
    {
        var englishText = request.EnglishText.Trim();
        var warnings = new List<string>();
        var inputGlosses = request.RussianGlosses?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];

        List<string> glosses;
        string partOfSpeech;
        string difficulty;
        List<string> examples;
        List<string> acceptedVariants;

        if (Lexicon.TryGetValue(englishText, out var entry))
        {
            glosses = inputGlosses.Count > 0 ? inputGlosses : entry.Glosses;
            partOfSpeech = InferPartOfSpeech(request.ItemKind, entry.Pos);
            difficulty = entry.Difficulty;
            examples = entry.Examples;
            acceptedVariants = [englishText, .. entry.Variants];
        }
        else
        {
            glosses = inputGlosses;
            partOfSpeech = InferPartOfSpeech(request.ItemKind, englishText.Contains(' ') ? "phrase" : "word");
            difficulty = englishText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 1 ? "B1" : "A2";
            examples = [CreateFallbackSentence(englishText)];
            acceptedVariants = [englishText];
            if (glosses.Count == 0)
            {
                warnings.Add("No free-tier translation was available. Add a Russian gloss manually for better quality.");
            }
        }

        var candidates = examples
            .Select(sentence => new ExampleCandidate(
                sentence,
                sentence.Replace(englishText, "____", StringComparison.OrdinalIgnoreCase),
                BuildTranslationHint(glosses),
                0.74,
                "ai-fallback"))
            .ToList();

        var validationWarnings = Validate(englishText, glosses, candidates);
        warnings.AddRange(validationWarnings);

        return new EnrichmentResponse(
            englishText,
            glosses,
            difficulty,
            partOfSpeech,
            candidates,
            warnings.Distinct().ToList(),
            acceptedVariants.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    public static IReadOnlyList<string> Validate(string englishText, List<string> glosses, List<ExampleCandidate> examples)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(englishText))
        {
            warnings.Add("English text is required.");
        }

        if (glosses.Any(gloss => gloss.Any(ch => ch is >= 'a' and <= 'z')))
        {
            warnings.Add("Russian glosses should not contain English words.");
        }

        foreach (var example in examples)
        {
            if (example.SentenceText.Length > 140)
            {
                warnings.Add("Example sentence is too long.");
            }

            if (!example.SentenceText.Contains(englishText, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add("Example sentence must include the target term.");
            }
        }

        return warnings;
    }

    private static string InferPartOfSpeech(string? requestedKind, string fallback) =>
        string.Equals(requestedKind, "phrase", StringComparison.OrdinalIgnoreCase) ? "phrase" : fallback;

    private static string BuildTranslationHint(List<string> glosses) =>
        glosses.Count == 0 ? "Добавьте перевод вручную" : string.Join(", ", glosses);

    private static string CreateFallbackSentence(string englishText)
    {
        var isPhrase = englishText.Contains(' ');
        return isPhrase
            ? $"Try to use {englishText} in a short everyday conversation."
            : $"I use the word {englishText} when I speak English.";
    }
}
