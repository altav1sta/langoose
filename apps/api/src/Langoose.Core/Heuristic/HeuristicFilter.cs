using Langoose.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Langoose.Core.Heuristic;

public sealed class HeuristicFilter(IOptions<HeuristicFilterSettings> options)
{
    private readonly HeuristicFilterSettings _settings = options.Value;

    public HeuristicVerdict Evaluate(string text, string pos)
    {
        if (text.Length < _settings.MinLength || text.Length > _settings.MaxLength)
            return new HeuristicVerdict(false, $"text length {text.Length} outside [{_settings.MinLength}, {_settings.MaxLength}]");

        if (!HasAllowedCharactersOnly(text))
            return new HeuristicVerdict(false, "text contains digits or disallowed characters");

        if (_settings.PosBlocklist.Contains(pos, StringComparer.OrdinalIgnoreCase))
            return new HeuristicVerdict(false, $"POS '{pos}' is on the blocklist");

        return new HeuristicVerdict(true, null);
    }

    private static bool HasAllowedCharactersOnly(string text)
    {
        foreach (var ch in text)
        {
            if (char.IsLetter(ch))
                continue;

            if (ch is '\'' or '-' or ' ')
                continue;

            return false;
        }

        return true;
    }
}
