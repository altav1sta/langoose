namespace Langoose.Core.Configuration;

public sealed class HeuristicFilterSettings
{
    public const string SectionName = "Heuristic";

    public int MinLength { get; init; }
    public int MaxLength { get; init; }
    public string[] PosBlocklist { get; init; } = [];
}
