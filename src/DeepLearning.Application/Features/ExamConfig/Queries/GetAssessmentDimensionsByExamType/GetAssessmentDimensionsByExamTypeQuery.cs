using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Queries.GetAssessmentDimensionsByExamType
{
    public record GetAssessmentDimensionsByExamTypeQuery(
        Guid ExamTypeId,
        TaskType? ApplicableTaskType) : IRequest<List<AssessmentDimensionResultItem>>;
}
