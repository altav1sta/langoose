using Langoose.Domain.Models;

namespace Langoose.Api.IntegrationTests.Infrastructure;

internal sealed class TestAppState
{
    public List<DictionaryItem> DictionaryItems { get; init; } = [];
    public List<ExampleSentence> ExampleSentences { get; init; } = [];
    public List<ReviewState> ReviewStates { get; init; } = [];
    public List<StudyEvent> StudyEvents { get; init; } = [];
    public List<ImportRecord> Imports { get; init; } = [];
    public List<ContentFlag> ContentFlags { get; init; } = [];
}
