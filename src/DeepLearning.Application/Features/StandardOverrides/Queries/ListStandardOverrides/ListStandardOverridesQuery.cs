using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.StandardOverrides.Queries.ListStandardOverrides
{
    public record ListStandardOverridesQuery(
        OverrideStatus? Status,
        Guid? ExamTypeId = null) : IRequest<List<StandardOverrideResultItem>>;
}
