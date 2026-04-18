using System.Text.Json;

namespace Langoose.Corpus.Data;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for corpus document
/// serialisation. Backed by source-generated metadata in
/// <see cref="CorpusJsonContext"/> — no reflection at runtime.
/// All corpus sources expose snake_case JSON; the matching naming policy
/// means model records can stay attribute-free.
/// </summary>
public static class CorpusJsonOptions
{
    public static JsonSerializerOptions Default { get; } = new(CorpusJsonContext.Default.Options);
}
