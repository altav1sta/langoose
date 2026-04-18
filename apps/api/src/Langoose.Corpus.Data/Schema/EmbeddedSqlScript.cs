namespace Langoose.Corpus.Data.Schema;

/// <summary>
/// A SQL script embedded as a resource in the assembly, identified by the
/// original file name (e.g. <c>001_metadata.sql</c>) so callers can apply
/// scripts in lexicographic order.
/// </summary>
public sealed record EmbeddedSqlScript(string FileName, string Sql);
