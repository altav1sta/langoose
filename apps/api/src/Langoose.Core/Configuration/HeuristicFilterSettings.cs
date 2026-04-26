namespace Langoose.Core.Configuration;

public sealed class HeuristicFilterSettings
{
    public int MinLength { get; init; } = 2;
    public int MaxLength { get; init; } = 300;
    public string[] PosBlocklist { get; init; } = ["name", "abbrev", "symbol", "intj"];
}
