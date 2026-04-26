using System.Text.Json.Serialization;
using Langoose.Domain.Models;

namespace Langoose.Data.Json;

/// <summary>
/// System.Text.Json source-generated metadata for the typed shape that
/// lands in <c>ImportEntry.Payload</c> JSONB column. Snake-case naming
/// matches the ADR's documented payload shape.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ImportPayload))]
[JsonSerializable(typeof(ImportPayloadEntry))]
[JsonSerializable(typeof(ImportPayloadSense))]
[JsonSerializable(typeof(ImportPayloadTranslation))]
public partial class ImportPayloadJsonContext : JsonSerializerContext;
