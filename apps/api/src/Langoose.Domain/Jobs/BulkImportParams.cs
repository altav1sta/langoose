namespace Langoose.Domain.Jobs;

/// <summary>
/// Per-invocation params for a bulk-import job. Held in
/// <c>BackgroundJob.Settings</c> JSONB. <see cref="Source"/> is the
/// snapshot identifier (e.g. <c>"wiktionary-2026-04-25"</c>); the handler
/// passes it through to the configured <c>IImportSourceReader</c>.
/// </summary>
public sealed record BulkImportParams(
    string Language,
    string Source);
