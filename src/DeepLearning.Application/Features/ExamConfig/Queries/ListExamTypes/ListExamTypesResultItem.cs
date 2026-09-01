using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.ExamConfig.Queries.ListExamTypes
{
    public record ListExamTypesResultItem(
        Guid Id,
        string Code,
        string Name,
        SubjectCategory SubjectCategory,
        string? Description,
        bool IsActive);
}
