using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Commands.AddFollowUpMessage
{
    /// <summary>Round 2+ of an open follow-up thread — see FollowUpThread's doc comment.</summary>
    public record AddFollowUpMessageCommand(Guid ThreadId, Guid UserId, string QuestionText) : IRequest<FollowUpThreadResult>;
}
