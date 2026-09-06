using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.ReviewLibrary.Queries.ListReviewVocab
{
    /// <summary>
    /// One canonical vocab entry (from <c>vocab_glossary</c>) for the cross-question review
    /// library — one row per distinct expression, with the AI-maintained account of every sense
    /// it has been seen in, plus this user's mastery.
    /// </summary>
    public record ReviewVocabResultItem(
        Guid Id,
        string EnglishExpr,
        string? ChineseEquiv,
        string AccumulatedSemantics,
        string? Category,
        string? Domain,
        string? Scenario,
        string? FrequencyTag,
        int SenseCount,
        int OccurrenceCount,
        int TimesEncountered,
        MasteryLevel MasteryLevel,
        DateTimeOffset? LastReviewedAt);
}
