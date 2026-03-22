using System.Text.Json.Serialization;

namespace Langoose.Api.Models;

public sealed class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? ProviderUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class SessionToken
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public string Token { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class DictionaryItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? OwnerId { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SourceType SourceType { get; set; }
    public string EnglishText { get; set; } = string.Empty;
    public List<string> RussianGlosses { get; set; } = [];
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ItemKind ItemKind { get; set; }
    public string PartOfSpeech { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "A1";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DictionaryItemStatus Status { get; set; } = DictionaryItemStatus.Active;
    public string CreatedByFlow { get; set; } = "seed";
    public string Notes { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public List<string> Distractors { get; set; } = [];
    public List<string> AcceptedVariants { get; set; } = [];
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class ExampleSentence
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ItemId { get; set; }
    public string SentenceText { get; set; } = string.Empty;
    public string ClozeText { get; set; } = string.Empty;
    public string TranslationHint { get; set; } = string.Empty;
    public double QualityScore { get; set; } = 0.7;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ContentOrigin Origin { get; set; } = ContentOrigin.Dataset;
}

public sealed class ReviewState
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ItemId { get; set; }
    public double Stability { get; set; } = 0.3;
    public DateTimeOffset DueAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int LapseCount { get; set; }
    public int SuccessCount { get; set; }
    public DateTimeOffset? LastSeenAtUtc { get; set; }
}

public sealed class StudyEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ItemId { get; set; }
    public DateTimeOffset AnsweredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string SubmittedAnswer { get; set; } = string.Empty;
    public string NormalizedAnswer { get; set; } = string.Empty;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public StudyVerdict Verdict { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FeedbackCode FeedbackCode { get; set; }
}

public sealed class ImportRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public int SkippedRows { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class ContentFlag
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ItemId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class DataStore
{
    public List<User> Users { get; set; } = [];
    public List<SessionToken> SessionTokens { get; set; } = [];
    public List<DictionaryItem> DictionaryItems { get; set; } = [];
    public List<ExampleSentence> ExampleSentences { get; set; } = [];
    public List<ReviewState> ReviewStates { get; set; } = [];
    public List<StudyEvent> StudyEvents { get; set; } = [];
    public List<ImportRecord> Imports { get; set; } = [];
    public List<ContentFlag> ContentFlags { get; set; } = [];
}