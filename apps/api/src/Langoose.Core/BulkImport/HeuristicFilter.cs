using Langoose.Core.Configuration;

namespace Langoose.Core.BulkImport;

/// <summary>
/// Pure, deterministic accept/reject pass over a candidate dictionary
/// entry's headword and POS. Runs inline at import time so that the AI
/// validator (later stage) only sees roughly-clean input.
///
/// Rules (all configurable through <see cref="HeuristicFilterSettings"/>):
/// length bounds; allowed character set (letters plus apostrophe, hyphen,
/// inner whitespace); POS blocklist. Order is fixed so reasons are
/// reproducible: length → characters → POS.
/// </summary>
public sealed class HeuristicFilter(HeuristicFilterSettings settings)
{
    public HeuristicVerdict Evaluate(string text, string pos)
    {
        if (text.Length < settings.MinLength || text.Length > settings.MaxLength)
            return new HeuristicVerdict(false, $"text length {text.Length} outside [{settings.MinLength}, {settings.MaxLength}]");

        if (!HasAllowedCharactersOnly(text))
            return new HeuristicVerdict(false, "text contains digits or disallowed characters");

        if (settings.PosBlocklist.Contains(pos, StringComparer.OrdinalIgnoreCase))
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
