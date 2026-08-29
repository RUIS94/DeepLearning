using MediatR;

namespace DeepLearning.Application.Features.Users.Queries.GetUserById
{
    public record GetUserByIdQuery(Guid Id) : IRequest<GetUserByIdResult>;
}
