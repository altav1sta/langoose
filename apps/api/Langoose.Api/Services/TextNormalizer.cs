using System.Text;
using System.Text.RegularExpressions;

namespace Langoose.Api.Services;

public static class TextNormalizer
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly HashSet<string> ArticleWords = ["a", "an", "the"];

    public static string NormalizeForComparison(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lowered = CleanInput(value).ToLowerInvariant();
        var builder = new StringBuilder(lowered.Length);

        foreach (var ch in lowered)
        {
            if (char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append(' ');
            }
        }

        return WhitespaceRegex.Replace(builder.ToString(), " ").Trim();
    }

    public static string CleanInput(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = value
            .Replace("\uFEFF", string.Empty)
            .Replace('\u2018', '\'')
            .Replace('\u2019', '\'')
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Trim();

        if (cleaned.Length >= 2 &&
            cleaned.StartsWith('"') &&
            cleaned.EndsWith('"'))
        {
            cleaned = cleaned[1..^1].Trim();
        }

        return cleaned;
    }

    public static List<string> NormalizeTokens(string value) =>
        NormalizeForComparison(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

    public static bool TokensMatchIgnoringArticles(string submitted, string expected)
    {
        var left = NormalizeTokens(submitted)
            .Where(token => !ArticleWords.Contains(token))
            .ToList();
        var right = NormalizeTokens(expected)
            .Where(token => !ArticleWords.Contains(token))
            .ToList();

        return left.SequenceEqual(right);
    }

    public static bool LooksLikeInflectionVariant(string submitted, string expected)
    {
        var left = NormalizeForComparison(submitted);
        var right = NormalizeForComparison(expected);

        if (left == right)
        {
            return true;
        }

        if (left.EndsWith("s") && left[..^1] == right)
        {
            return true;
        }

        if (right.EndsWith("s") && right[..^1] == left)
        {
            return true;
        }

        if (left.EndsWith("ed") && left[..^2] == right)
        {
            return true;
        }

        if (left.EndsWith("ing") && left[..^3] == right)
        {
            return true;
        }

        return false;
    }

    public static bool LooksLikeMinorTypo(string submitted, string expected)
    {
        var left = NormalizeForComparison(submitted);
        var right = NormalizeForComparison(expected);

        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var tokenizedLeft = NormalizeTokens(left);
        var tokenizedRight = NormalizeTokens(right);

        if (tokenizedLeft.Count == tokenizedRight.Count && tokenizedLeft.Count > 1)
        {
            return tokenizedLeft
                .Zip(tokenizedRight, (l, r) => LevenshteinDistance(l, r))
                .All(distance => distance <= 1);
        }

        var distance = LevenshteinDistance(left, right);
        var threshold = Math.Max(1, right.Length >= 7 ? 2 : 1);

        return distance <= threshold;
    }

    private static int LevenshteinDistance(string source, string target)
    {
        var dp = new int[source.Length + 1, target.Length + 1];

        for (var i = 0; i <= source.Length; i++)
        {
            dp[i, 0] = i;
        }

        for (var j = 0; j <= target.Length; j++)
        {
            dp[0, j] = j;
        }

        for (var i = 1; i <= source.Length; i++)
        {
            for (var j = 1; j <= target.Length; j++)
            {
                var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[source.Length, target.Length];
    }
}
