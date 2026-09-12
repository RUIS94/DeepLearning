using MediatR;

namespace DeepLearning.Application.Features.Users.Queries.ListUsers
{
    /// <summary>Admin-only (ref/管理员与用户权限隔离_策划书.md A3). Page is 1-based.</summary>
    public record ListUsersQuery(int Page = 1, int PageSize = 50) : IRequest<ListUsersResult>;
}
