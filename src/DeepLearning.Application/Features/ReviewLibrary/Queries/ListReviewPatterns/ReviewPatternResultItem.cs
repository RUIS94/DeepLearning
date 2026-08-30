using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.ReviewLibrary.Queries.ListReviewPatterns
{
    /// <summary>QuestionId lets the frontend jump back to the pattern's original question context (design doc §2.2's "点击某条记录跳转回其原题目上下文").</summary>
    public record ReviewPatternResultItem(
        Guid Id,
        Guid? QuestionId,
        string PatternName,
        string? ExampleSentence,
        string? Domain,
        string? Scenario,
        string? FrequencyTag,
        int TimesEncountered,
        MasteryLevel MasteryLevel,
        DateTimeOffset? LastReviewedAt);
}
