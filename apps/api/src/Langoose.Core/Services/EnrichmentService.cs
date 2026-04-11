using Langoose.Core.Utilities;
using Langoose.Domain.Constants;
using Langoose.Domain.Models;
using Langoose.Domain.Services;

namespace Langoose.Core.Services;

public sealed class EnrichmentService : IEnrichmentService
{
    private static readonly Dictionary<string, LexiconEntry> Lexicon =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["book"] = new(
                ["\u043A\u043D\u0438\u0433\u0430"],
                "noun",
                "A1",
                ["She bought a new book yesterday."],
                []),
            ["decision"] = new(
                ["\u0440\u0435\u0448\u0435\u043D\u0438\u0435"],
                "noun",
                "B1",
                ["Making that decision took a long time."],
                ["choice"]),
            ["take care of"] = new(
                ["\u0437\u0430\u0431\u043E\u0442\u0438\u0442\u044C\u0441\u044F \u043E"],
                "phrase",
                "B1",
                ["I take care of my little brother after school."],
                ["look after"]),
            ["look for"] = new(
                ["\u0438\u0441\u043A\u0430\u0442\u044C"],
                "phrase",
                "A2",
                ["We are looking for a small apartment."],
                ["search for"]),
            ["improve"] = new(
                ["\u0443\u043B\u0443\u0447\u0448\u0430\u0442\u044C"],
                "verb",
                "B1",
                ["Practice every day to improve your English."],
                ["get better"])
        };

    public EnrichmentResult Enrich(EnrichmentInput input)
    {
        var englishText = input.EnglishText.Trim();
        List<string> warnings = [];
        var inputGlosses = input.RussianGlosses?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        List<string> glosses;
        string partOfSpeech;
        string difficulty;
        List<string> examples;
        List<string> acceptedVariants;

        if (Lexicon.TryGetValue(englishText, out var entry))
        {
            glosses = inputGlosses.Count > 0 ? inputGlosses : entry.Glosses;
            partOfSpeech = InferPartOfSpeech(input.ItemKind, entry.PartOfSpeech);
            difficulty = entry.Difficulty;
            examples = entry.Examples;
            acceptedVariants = [englishText, .. entry.Variants];
        }
        else
        {
            glosses = inputGlosses;
            partOfSpeech = InferPartOfSpeech(
                input.ItemKind,
                englishText.Contains(' ') ? "phrase" : "word");
            difficulty = englishText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 1
                ? "B1"
                : "A2";
            examples = [CreateFallbackSentence(englishText)];
            acceptedVariants = [englishText];

            if (glosses.Count == 0)
            {
                warnings.Add(
                    "No free-tier translation was available. Add a Russian gloss manually for better quality.");
            }
        }

        List<ExampleCandidate> candidates = [.. examples
            .Select(sentence => new ExampleCandidate(
                sentence,
                sentence.Replace(englishText, "____", StringComparison.OrdinalIgnoreCase),
                BuildTranslationHint(glosses),
                ExampleQualityScores.EnrichmentFallback,
                "ai-fallback"))];

        var validationWarnings = Validate(englishText, glosses, candidates);
        warnings.AddRange(validationWarnings);

        return new EnrichmentResult(
            englishText,
            glosses,
            difficulty,
            partOfSpeech,
            candidates,
            [.. warnings.Distinct()],
            [.. acceptedVariants.Distinct(StringComparer.OrdinalIgnoreCase)]);
    }

    public static IReadOnlyList<string> Validate(
        string englishText,
        List<string> glosses,
        List<ExampleCandidate> examples)
    {
        List<string> warnings = [];

        if (string.IsNullOrWhiteSpace(englishText))
        {
            warnings.Add("English text is required.");
        }

        if (glosses.Any(x => x.Any(ch => ch is >= 'a' and <= 'z')))
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
        string.Equals(requestedKind, "phrase", StringComparison.OrdinalIgnoreCase)
            ? "phrase"
            : fallback;

    private static string BuildTranslationHint(List<string> glosses) =>
        glosses.Count == 0
            ? "\u0414\u043E\u0431\u0430\u0432\u044C\u0442\u0435 " +
                "\u043F\u0435\u0440\u0435\u0432\u043E\u0434 \u0432\u0440\u0443\u0447\u043D\u0443\u044E"
            : string.Join(", ", glosses);

    private static string CreateFallbackSentence(string englishText)
    {
        var isPhrase = englishText.Contains(' ');

        return isPhrase
            ? $"Try to use {englishText} in a short everyday conversation."
            : $"I use the word {englishText} when I speak English.";
    }

    private sealed record LexiconEntry(
        List<string> Glosses,
        string PartOfSpeech,
        string Difficulty,
        List<string> Examples,
        List<string> Variants);
}
