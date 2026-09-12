using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.Users.Queries.GetUserById
{
    public record GetUserByIdResult(
        Guid Id,
        string Username,
        string Email,
        string? DisplayName,
        string LanguagePreference,
        UserRole Role,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastLoginAt);
}
