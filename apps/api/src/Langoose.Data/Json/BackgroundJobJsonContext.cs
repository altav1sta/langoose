using System.Text.Json.Serialization;
using Langoose.Domain.Jobs;

namespace Langoose.Data.Json;

/// <summary>
/// System.Text.Json source-generated metadata for the typed shapes that
/// land in <c>BackgroundJob.Settings</c> and <c>BackgroundJob.ExecutionState</c>
/// JSONB columns. Snake-case naming matches the documented JSON shape.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BulkImportParams))]
[JsonSerializable(typeof(BulkImportState))]
[JsonSerializable(typeof(BulkImportCursor))]
public partial class BackgroundJobJsonContext : JsonSerializerContext;
