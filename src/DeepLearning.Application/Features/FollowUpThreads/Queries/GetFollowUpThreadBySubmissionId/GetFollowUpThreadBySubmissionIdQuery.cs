using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Queries.GetFollowUpThreadBySubmissionId
{
    /// <summary>Throws NotFoundException (404) when the submission has no thread yet — the frontend treats that as "no dispute started", not an error.</summary>
    public record GetFollowUpThreadBySubmissionIdQuery(Guid SubmissionId) : IRequest<FollowUpThreadResult>;
}
