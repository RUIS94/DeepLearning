using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.Questions.Queries.GetQuestionById
{
    public record MeaningCheckpointItem(Guid Id, string CheckpointText, string? CheckpointType, CheckpointImportance Importance);

    public record SeededErrorItem(
        Guid Id,
        int PositionStart,
        int PositionEnd,
        Guid ErrorTaxonomyId,
        string ErrorCategoryKey,
        string CorrectReferenceText,
        string? Note);

    // Non-null only for TaskType.B questions — this is the field the API test
    // asserts on to prove TaskA/TaskB get genuinely different response shapes.
    public record TaskBDetails(string FlawedTranslationText, List<SeededErrorItem> SeededErrors);

    public record GetQuestionByIdResult(
        Guid Id,
        TaskType TaskType,
        Difficulty Difficulty,
        string Title,
        string? Brief,
        string SourceText,
        int? WordCount,
        QuestionOrigin Origin,
        SourceType SourceType,
        bool IsSeedReference,
        bool InBank,
        Visibility Visibility,
        Guid? CreatedBy,
        DateTimeOffset CreatedAt,
        bool IsActive,
        List<MeaningCheckpointItem> MeaningCheckpoints,
        TaskBDetails? TaskB,
        List<Guid> CategoryIds);
}
