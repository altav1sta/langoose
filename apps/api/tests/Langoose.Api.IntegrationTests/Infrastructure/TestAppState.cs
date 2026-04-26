using Langoose.Domain.Models;

namespace Langoose.Api.IntegrationTests.Infrastructure;

internal sealed class TestAppState
{
    public List<DictionaryEntry> DictionaryEntries { get; init; } = [];
    public List<EntryContext> EntryContexts { get; init; } = [];
    public List<UserEntry> UserEntries { get; init; } = [];
    public List<UserProgress> UserProgress { get; init; } = [];
    public List<StudyEvent> StudyEvents { get; init; } = [];
    public List<UserImport> UserImports { get; init; } = [];
    public List<ContentFlag> ContentFlags { get; init; } = [];
}
