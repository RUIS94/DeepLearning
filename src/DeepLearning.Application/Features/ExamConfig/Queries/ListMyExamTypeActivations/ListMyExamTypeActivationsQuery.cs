using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Queries.ListMyExamTypeActivations
{
    /// <summary>
    /// Every globally-active exam type (admin's ExamType.IsActive), each annotated with whether
    /// the caller has personally activated it (U3, ref/管理员与用户权限隔离_策划书.md Phase 4).
    /// A globally-inactive exam type is omitted entirely — it isn't selectable by anyone right now.
    /// </summary>
    public record ListMyExamTypeActivationsQuery(Guid UserId) : IRequest<List<ExamTypeActivationResultItem>>;

    public record ExamTypeActivationResultItem(Guid ExamTypeId, string Code, string Name, bool IsActivatedByMe);
}
