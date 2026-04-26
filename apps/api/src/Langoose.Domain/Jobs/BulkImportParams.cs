namespace Langoose.Domain.Jobs;

public sealed record BulkImportParams(
    string Language,
    string WiktionarySource,
    string WordfreqSource,
    int? TopFrequencyRank,
    int? Limit,
    Guid? RequestedByUserId);
