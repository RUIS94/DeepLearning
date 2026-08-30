namespace DeepLearning.Application.Features.Questions.Commands.GenerateDeepLearningContent
{
    public record GenerateDeepLearningContentResult(
        Guid QuestionId,
        string ReferenceText,
        string? ComparisonNotes,
        List<SentencePatternItem> SentencePatterns,
        List<VocabExpressionItem> VocabExpressions,
        bool WasCached);

    public record SentencePatternItem(
        Guid Id,
        string PatternName,
        string? ExampleSentence,
        string? BreakdownSteps,
        string? Variants,
        string? Domain,
        string? Scenario,
        string? FrequencyTag);

    public record VocabExpressionItem(
        Guid Id,
        string EnglishExpr,
        string? ChineseEquiv,
        string? ContextNote,
        string? Category,
        string? Domain,
        string? Scenario,
        string? FrequencyTag);
}
