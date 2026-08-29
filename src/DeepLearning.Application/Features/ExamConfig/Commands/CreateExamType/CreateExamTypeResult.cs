using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType
{
    public record CreateExamTypeResult(
        Guid Id,
        string Code,
        string Name,
        SubjectCategory SubjectCategory,
        bool IsActive,
        DateTimeOffset CreatedAt);
}
