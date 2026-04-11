namespace Langoose.Domain.Models;

public sealed class ContextTranslation
{
    public required Guid SourceContextId { get; init; }
    public required Guid TargetContextId { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public EntryContext SourceContext { get; set; } = null!;
    public EntryContext TargetContext { get; set; } = null!;
}
