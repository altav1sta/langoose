using System.Text.Json.Serialization;
using Langoose.Corpus.Data.Models;

namespace Langoose.Corpus.Data;

/// <summary>
/// System.Text.Json source-generated metadata for corpus document types.
/// Avoids reflection at runtime, compile-checks that all serialised types
/// are reachable, and is AOT-friendly.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(WiktionaryEntry))]
[JsonSerializable(typeof(WiktionaryForm))]
[JsonSerializable(typeof(WiktionarySense))]
[JsonSerializable(typeof(WiktionaryTranslation))]
public partial class CorpusJsonContext : JsonSerializerContext;
