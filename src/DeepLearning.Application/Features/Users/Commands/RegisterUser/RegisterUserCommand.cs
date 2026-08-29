using MediatR;

namespace DeepLearning.Application.Features.Users.Commands.RegisterUser
{
    public record RegisterUserCommand(
        string Username,
        string Email,
        string Password,
        string? DisplayName) : IRequest<RegisterUserResult>;
}
