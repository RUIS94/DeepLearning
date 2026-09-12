using MediatR;

namespace DeepLearning.Application.Features.Questions.Queries.GetQuestionById
{
    /// <summary>
    /// RequesterId gates access (ref/管理员与用户权限隔离_策划书.md U4): a non-Shared question not
    /// created by RequesterId 404s rather than revealing it exists but isn't theirs.
    /// </summary>
    public record GetQuestionByIdQuery(Guid Id, Guid RequesterId) : IRequest<GetQuestionByIdResult>;
}
