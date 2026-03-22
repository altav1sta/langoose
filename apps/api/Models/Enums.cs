namespace Langoose.Api.Models;

public enum SourceType
{
    Base,
    Custom
}

public enum ItemKind
{
    Word,
    Phrase
}

public enum ContentOrigin
{
    Dataset,
    Ai,
    Manual
}

public enum DictionaryItemStatus
{
    Active,
    Flagged,
    Archived
}

public enum StudyVerdict
{
    Correct,
    AlmostCorrect,
    Incorrect
}

public enum FeedbackCode
{
    ExactMatch,
    AcceptedVariant,
    MeaningMismatch,
    MissingArticle,
    InflectionMismatch,
    MinorTypo
}
