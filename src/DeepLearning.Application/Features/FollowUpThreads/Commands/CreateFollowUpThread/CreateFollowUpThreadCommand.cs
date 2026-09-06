using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Commands.CreateFollowUpThread
{
    /// <summary>
    /// Starts the (at most one open) follow-up thread for a submission — the first round.
    /// ContextRef optionally points at the specific grading_result/error_list id the user is
    /// disputing (design doc §6.7); opaque to this handler, just persisted and passed to the AI.
    ///
    /// Kind selects the track (see <see cref="FollowUpThreadKind"/>): null means "infer" —
    /// dispute if ContextRef is set, else knowledge. Pass score_challenge explicitly (from the
    /// score display) together with DimensionId, the assessment dimension whose Band is being
    /// challenged; the closing call may then re-grade that dimension.
    /// </summary>
    public record CreateFollowUpThreadCommand(
        Guid SubmissionId,
        Guid UserId,
        Guid ExamTypeId,
        string? ContextRef,
        string QuestionText,
        FollowUpThreadKind? Kind = null,
        Guid? DimensionId = null) : IRequest<FollowUpThreadResult>;
}
