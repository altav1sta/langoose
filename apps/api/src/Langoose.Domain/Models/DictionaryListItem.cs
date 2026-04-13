using Langoose.Domain.Enums;

namespace Langoose.Domain.Models;

public sealed record DictionaryListItem(
    Guid DictionaryEntryId,
    string Text,
    string Language,
    string? Difficulty,
    bool IsPublic,
    Guid? UserDictionaryEntryId,
    EnrichmentStatus? EnrichmentStatus,
    string? PartOfSpeech,
    string? Notes,
    List<string> Tags);
