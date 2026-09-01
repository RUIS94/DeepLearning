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
    /// exam sample, not an ordinary user-authored one. There is no auth/role system anywhere in
    /// this codebase (out of scope, same as login/JWT generally) — like every other
    /// caller-supplied field here (Visibility, CreatedBy), this is trust-based, not gated behind
    /// an admin check that doesn't exist.
    /// </summary>
    public record ImportUserQuestionCommand(
        TaskType TaskType,
        Difficulty Difficulty,
        string Title,
        string? Brief,
        string SourceText,
        string? FlawedTranslationText,
        Guid? CreatedBy,
        Visibility Visibility,
        List<MeaningCheckpointInput> MeaningCheckpoints,
        List<SeededErrorInput> SeededErrors,
        bool IsSeedReference = false) : IRequest<ImportUserQuestionResult>;
}
