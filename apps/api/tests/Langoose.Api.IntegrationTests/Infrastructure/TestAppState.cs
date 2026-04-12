using Langoose.Domain.Models;

namespace Langoose.Api.IntegrationTests.Infrastructure;

internal sealed class TestAppState
{
    public List<DictionaryEntry> DictionaryEntries { get; init; } = [];
    public List<EntryContext> EntryContexts { get; init; } = [];
    public List<UserDictionaryEntry> UserDictionaryEntries { get; init; } = [];
    public List<UserProgress> UserProgress { get; init; } = [];
    public List<StudyEvent> StudyEvents { get; init; } = [];
    public List<ImportRecord> ImportRecords { get; init; } = [];
    public List<ContentFlag> ContentFlags { get; init; } = [];
}
