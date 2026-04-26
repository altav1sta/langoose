using Langoose.Corpus.Data.Models;

namespace Langoose.Corpus.Data.Readers;

public sealed record WiktionaryBundle(
    int Rank,
    string Language,
    string Word,
    string Pos,
    WiktionaryEntryRow[] Rows);
