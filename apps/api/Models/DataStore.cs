namespace Langoose.Api.Models;

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
