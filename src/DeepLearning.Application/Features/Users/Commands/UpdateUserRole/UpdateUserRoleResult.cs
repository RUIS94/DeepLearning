using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.Users.Commands.UpdateUserRole
{
    public record UpdateUserRoleResult(Guid Id, UserRole Role);
}
