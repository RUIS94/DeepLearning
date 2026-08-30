using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.ReviewLibrary.Queries.ListReviewVocab
{
    public record ReviewVocabResultItem(
        Guid Id,
        Guid? QuestionId,
        string EnglishExpr,
        string? ChineseEquiv,
        string? Domain,
        string? Scenario,
        string? FrequencyTag,
        int TimesEncountered,
        MasteryLevel MasteryLevel,
        DateTimeOffset? LastReviewedAt);
}
