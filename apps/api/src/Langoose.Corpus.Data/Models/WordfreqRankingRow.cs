namespace Langoose.Corpus.Data.Models;

public sealed record WordfreqRankingRow(
    string LangCode,
    string Word,
    int Rank,
    decimal ZipfScore,
    string Source);
