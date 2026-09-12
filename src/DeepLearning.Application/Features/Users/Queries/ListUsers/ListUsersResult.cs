using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.Users.Queries.ListUsers
{
    public record ListUsersResultItem(
        Guid Id,
        string Username,
        string Email,
        string? DisplayName,
        UserRole Role,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastLoginAt);

    public record ListUsersResult(List<ListUsersResultItem> Items, int TotalCount, int Page, int PageSize);
}
