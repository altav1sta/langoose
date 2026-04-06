namespace Langoose.Api.Configuration;

public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public bool Enabled { get; init; }

    public string[] KnownProxies { get; init; } = [];

    public string[] KnownNetworks { get; init; } = [];
}
