using System.Reflection;

namespace Langoose.Corpus.Data.Schema;

/// <summary>
/// Discovers the embedded SQL schema files shipped with this assembly,
/// returned in lexicographic name order so DDL applies in the intended order
/// (001_metadata.sql before 002_wiktionary.sql, etc.).
/// </summary>
public static class CorpusSchema
{
    private const string ResourcePrefix = "Langoose.Corpus.Data.Schema.";
    private const string SqlSuffix = ".sql";

    public static IReadOnlyList<EmbeddedSqlScript> GetSchemaScripts()
    {
        var assembly = typeof(CorpusSchema).Assembly;
        var scripts = new List<EmbeddedSqlScript>();

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                || !resourceName.EndsWith(SqlSuffix, StringComparison.Ordinal))
                continue;

            var fileName = resourceName[ResourcePrefix.Length..];
            var sql = ReadResource(assembly, resourceName);

            scripts.Add(new EmbeddedSqlScript(fileName, sql));
        }

        scripts.Sort((x, y) => string.CompareOrdinal(x.FileName, y.FileName));

        return scripts;
    }

    private static string ReadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found in {assembly.GetName().Name}.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
