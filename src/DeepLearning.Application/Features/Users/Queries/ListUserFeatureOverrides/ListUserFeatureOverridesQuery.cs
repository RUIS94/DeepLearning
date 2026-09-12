using MediatR;

namespace DeepLearning.Application.Features.Users.Queries.ListUserFeatureOverrides
{
    /// <summary>Admin-only (ref/管理员与用户权限隔离_策划书.md A4) — this user's existing per-feature overrides, if any.</summary>
    public record ListUserFeatureOverridesQuery(Guid UserId) : IRequest<List<ListUserFeatureOverridesResultItem>>;

    public record ListUserFeatureOverridesResultItem(string FeatureKey, bool Enabled);
}
