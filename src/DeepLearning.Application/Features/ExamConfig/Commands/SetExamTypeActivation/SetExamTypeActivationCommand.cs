using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Commands.SetExamTypeActivation
{
    /// <summary>
    /// U3 (ref/管理员与用户权限隔离_策划书.md Phase 4) — a user adding/removing an exam type from
    /// their own personal practice scope. Independent of ExamType.IsActive, which is the
    /// admin-controlled system-wide switch (see UserExamTypeActivation's own doc comment);
    /// activating an exam type the admin has turned off is rejected rather than silently accepted.
    /// </summary>
    public record SetExamTypeActivationCommand(Guid UserId, Guid ExamTypeId, bool IsActive)
        : IRequest<SetExamTypeActivationResult>;
}
