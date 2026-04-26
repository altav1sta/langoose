using System.Text.Json;
using System.Text.Json.Nodes;
using Langoose.Corpus.Data;
using Langoose.Corpus.Data.Readers;
using Langoose.Data.Json;
using Langoose.Domain.Models;

namespace Langoose.Core.BulkImport;

/// <summary>
/// Converts one corpus bundle (multiple Wiktionary rows sharing a
/// language/word/POS, typically split by etymology) into a single import
/// payload bundle: header + flattened senses with sense-scoped
/// translations + the raw source rows preserved verbatim.
///
/// Senses are concatenated across rows in input order with a continuous
/// <c>SenseIndex</c>. The factory is pure; the reader and persistence
/// layer are the caller's job.
/// </summary>
public sealed class ImportPayloadFactory
{
    public string Build(WiktionaryBundle bundle)
    {
        var senses = new List<ImportPayloadSense>();
        var senseIndex = 0;

        foreach (var row in bundle.Rows)
        {
            var entry = JsonSerializer.Deserialize(
                row.DataJson, CorpusJsonContext.Default.WiktionaryEntry);

            if (entry?.Senses is null)
                continue;

            foreach (var sense in entry.Senses)
            {
                var translations = (sense.Translations ?? [])
                    .Where(x => !string.IsNullOrWhiteSpace(x.Code) && !string.IsNullOrWhiteSpace(x.Word))
                    .Select(x => new ImportPayloadTranslation(x.Code!, x.Word!, null))
                    .ToArray();
                var gloss = sense.Glosses?.FirstOrDefault();

                senses.Add(new ImportPayloadSense(senseIndex++, gloss, translations));
            }
        }

        var payload = new ImportPayload(
            new ImportPayloadEntry(bundle.Language, bundle.Word, bundle.Pos),
            senses.ToArray());

        var payloadObject = JsonSerializer.SerializeToNode(
            payload, ImportPayloadJsonContext.Default.ImportPayload)!.AsObject();

        var rawArray = new JsonArray();
        foreach (var row in bundle.Rows)
        {
            var rawNode = JsonNode.Parse(row.DataJson);
            if (rawNode is not null)
                rawArray.Add(rawNode);
        }

        payloadObject["raw"] = rawArray;

        return payloadObject.ToJsonString();
    }
}
