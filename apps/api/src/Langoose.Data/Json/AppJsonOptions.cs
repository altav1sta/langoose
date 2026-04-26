using System.Text.Json;
using System.Text.Json.Serialization;

namespace Langoose.Data.Json;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for any JSON that flows
/// through the app database — JSONB columns
/// (<c>BackgroundJob.Settings</c>, <c>BackgroundJob.ExecutionState</c>,
/// <c>ImportEntry.Payload</c>, …) and the typed payloads that get
/// (de)serialised into them. Snake-case property names, nulls skipped on
/// write.
/// </summary>
public static class AppJsonOptions
{
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
