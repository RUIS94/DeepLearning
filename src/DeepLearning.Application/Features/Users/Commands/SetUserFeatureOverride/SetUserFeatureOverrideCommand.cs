using MediatR;

namespace DeepLearning.Application.Features.Users.Commands.SetUserFeatureOverride
{
    /// <summary>
    /// Admin-only (ref/管理员与用户权限隔离_策划书.md A4). Enabled=null clears the override so the
    /// user falls back to the global feature_flags value; a non-null value pins this user's
    /// access to that feature independent of the global flag.
    /// </summary>
    public record SetUserFeatureOverrideCommand(Guid UserId, string FeatureKey, bool? Enabled) : IRequest<SetUserFeatureOverrideResult>;
}
