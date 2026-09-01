using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Commands.CloseFollowUpThread
{
    /// <summary>
    /// User-triggered "结束追问". Runs the separate summary AI call that decides FinalVerdict,
    /// whether a StandardOverride gets created, and where the submission ends up — see
    /// FollowUpThread's doc comment. Never reopened afterwards (single-thread-per-submission).
    /// </summary>
    public record CloseFollowUpThreadCommand(Guid ThreadId, Guid UserId) : IRequest<FollowUpThreadResult>;
}
