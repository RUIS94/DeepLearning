using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Commands.CreateFollowUpThread
{
    /// <summary>
    /// Starts the (at most one, ever) follow-up thread for a submission — the first round.
    /// ContextRef optionally points at the specific grading_result/error_list id the user is
    /// disputing (design doc §6.7), same as the retired single-shot CreateFollowUpQuestionCommand;
    /// opaque to this handler, just persisted and passed to the AI as extra context for every
    /// round in the thread.
    /// </summary>
    public record CreateFollowUpThreadCommand(
        Guid SubmissionId,
        Guid UserId,
        Guid ExamTypeId,
        string? ContextRef,
        string QuestionText) : IRequest<FollowUpThreadResult>;
}
