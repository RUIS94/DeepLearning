using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.Users.Commands.UpdateUserRole
{
    /// <summary>Admin-only (ref/管理员与用户权限隔离_策划书.md A3) — the ongoing way to grant/revoke admin, replacing the Phase 0 bootstrap config.</summary>
    public record UpdateUserRoleCommand(Guid Id, UserRole Role) : IRequest<UpdateUserRoleResult>;
}
