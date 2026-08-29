using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion
{
    public record MeaningCheckpointInput(string CheckpointText, string? CheckpointType, CheckpointImportance Importance);

    public record SeededErrorInput(
        int PositionStart,
        int PositionEnd,
        Guid ErrorTaxonomyId,
        string CorrectReferenceText,
        string? Note);

    public record ImportUserQuestionCommand(
        TaskType TaskType,
        Difficulty Difficulty,
        string Title,
        string? Brief,
        string SourceText,
        string? FlawedTranslationText,
        int? WordCount,
        Guid? CreatedBy,
        Visibility Visibility,
        List<MeaningCheckpointInput> MeaningCheckpoints,
        List<SeededErrorInput> SeededErrors) : IRequest<ImportUserQuestionResult>;
}
