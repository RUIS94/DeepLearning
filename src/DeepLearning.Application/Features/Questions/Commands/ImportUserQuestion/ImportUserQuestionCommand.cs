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

    /// <summary>
    /// IsSeedReference is optional, defaults to false. This is currently the ONLY way to get a
    /// real-exam sample into the pool ListSeedReferenceCandidatesAsync/manual SeedQuestionIds
    /// draw from (design doc §11.2 Step 8) — GenerateQuestionCommandHandler always creates its
    /// own output with IsSeedReference=false. When true, the handler also sets
    /// Origin=real_exam_seed/SourceType=real_exam instead of the usual user_uploaded/
    /// user_generated pair, since a question imported this way is meant to represent an actual
    /// exam sample, not an ordinary user-authored one.
    ///
    /// Visibility is NOT a caller-supplied field (ref/管理员与用户权限隔离_策划书.md D2'/A2') — the
    /// handler derives it from IsSeedReference: true → Shared, false → Private. The only path
    /// that can produce a Shared question at all is QuestionsController.Import gating
    /// IsSeedReference=true behind AdminOnly, matching GenerateQuestionCommandHandler's own
    /// hardcoded Visibility=Private for AI-generated questions.
    /// </summary>
    public record ImportUserQuestionCommand(
        TaskType TaskType,
        Difficulty Difficulty,
        string Title,
        string? Brief,
        string SourceText,
        string? FlawedTranslationText,
        Guid? CreatedBy,
        List<MeaningCheckpointInput> MeaningCheckpoints,
        List<SeededErrorInput> SeededErrors,
        bool IsSeedReference = false) : IRequest<ImportUserQuestionResult>;
}
