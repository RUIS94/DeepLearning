namespace DeepLearning.Application.Features.Users.Commands.RegisterUser
{
    public record RegisterUserResult(Guid Id, string Username, string Email, DateTimeOffset CreatedAt);
}
