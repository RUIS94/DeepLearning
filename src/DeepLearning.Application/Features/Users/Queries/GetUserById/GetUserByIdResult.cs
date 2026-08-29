namespace DeepLearning.Application.Features.Users.Queries.GetUserById
{
    public record GetUserByIdResult(
        Guid Id,
        string Username,
        string Email,
        string? DisplayName,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastLoginAt);
}
