using System.Text.Json.Serialization;

namespace Langoose.Api.Models;

public sealed record EmailSignInRequest(string Email, string? Name);
public sealed record SocialSignInRequest(string Provider, string ProviderUserId, string Email, string? Name);
public sealed record AuthResponse(Guid UserId, string Email, string Name, string Token);

public sealed record DictionaryItemRequest(
    string EnglishText,
    List<string>? RussianGlosses,
    string? ItemKind,
    string? PartOfSpeech,
    string? Difficulty,
    string? Notes,
    List<string>? Tags,
    string? CreatedByFlow,
    bool GenerateExamples = true);

public sealed record DictionaryItemPatchRequest(
    List<string>? RussianGlosses,
    string? PartOfSpeech,
    string? Difficulty,
    string? Notes,
    List<string>? Tags,
    string? Status);

public sealed record EnrichmentRequest(string EnglishText, List<string>? RussianGlosses, string? ItemKind);
public sealed record EnrichmentResponse(
    string EnglishText,
    List<string> RussianGlosses,
    string Difficulty,
    string PartOfSpeech,
    List<ExampleCandidate> Examples,
    List<string> ValidationWarnings,
    List<string> AcceptedVariants);

public sealed record ExampleCandidate(string SentenceText, string ClozeText, string TranslationHint, double QualityScore, string Origin);
public sealed record StudyCardResponse(Guid ItemId, string Prompt, string TranslationHint, List<string> Glosses, string ItemKind, string SourceType, string Difficulty);
public sealed record StudyAnswerRequest(Guid ItemId, string SubmittedAnswer);
public sealed record StudyAnswerResult(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] StudyVerdict Verdict,
    string NormalizedAnswer,
    string? AcceptedVariant,
    string ExpectedAnswer,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] FeedbackCode FeedbackCode,
    DateTimeOffset NextDueAtUtc);
public sealed record ReportIssueRequest(Guid ItemId, string Reason, string? Details);
public sealed record ImportCsvRequest(string FileName, string CsvContent);
public sealed record ImportCsvResponse(int TotalRows, int ImportedRows, int SkippedRows, List<string> Errors);
public sealed record ProgressDashboardResponse(int TotalItems, int DueNow, int NewItems, int BaseItems, int CustomItems, int StudiedToday);
public sealed record MeResponse(Guid UserId, string Email, string Name);
