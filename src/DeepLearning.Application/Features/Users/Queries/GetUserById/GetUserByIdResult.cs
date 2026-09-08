namespace DeepLearning.Application.Features.Users.Queries.GetUserById
{
    public record GetUserByIdResult(
        Guid Id,
        string Username,
        string Email,
        string? DisplayName,
        string LanguagePreference,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastLoginAt);
}
