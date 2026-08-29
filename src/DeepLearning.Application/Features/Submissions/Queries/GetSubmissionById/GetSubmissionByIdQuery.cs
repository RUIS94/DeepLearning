using MediatR;

namespace DeepLearning.Application.Features.Submissions.Queries.GetSubmissionById
{
    public record GetSubmissionByIdQuery(Guid Id) : IRequest<GetSubmissionByIdResult>;
}
