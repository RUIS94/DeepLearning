namespace DeepLearning.Application.Features.Questions.Queries.GetDeepLearningContentByQuestionId
{
    public record GetDeepLearningContentByQuestionIdResult(
        Guid QuestionId,
        string ReferenceText,
        string? ComparisonNotes,
        List<SentencePatternResultItem> SentencePatterns,
        List<VocabExpressionResultItem> VocabExpressions);

    public record SentencePatternResultItem(
        Guid Id,
        string PatternName,
        string? ExampleSentence,
        string? BreakdownSteps,
        string? Variants,
        string? Domain,
        string? Scenario,
        string? FrequencyTag);

    public record VocabExpressionResultItem(
        Guid Id,
        string EnglishExpr,
        string? ChineseEquiv,
        string? ContextNote,
        string? Category,
        string? Domain,
        string? Scenario,
        string? FrequencyTag,
        bool? LiteralTranslatable);
}
