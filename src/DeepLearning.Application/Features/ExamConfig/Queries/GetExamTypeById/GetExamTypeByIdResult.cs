using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.ExamConfig.Queries.GetExamTypeById
{
    public record GetExamTypeByIdResult(
        Guid Id,
        string Code,
        string Name,
        SubjectCategory SubjectCategory,
        string? SourceLanguage,
        string? TargetLanguage,
        string? GradeLevel,
        string? Description,
        bool IsActive,
        DateTimeOffset CreatedAt);
}
