using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType
{
    public record CreateExamTypeCommand(
        string Code,
        string Name,
        SubjectCategory SubjectCategory,
        string? SourceLanguage,
        string? TargetLanguage,
        string? GradeLevel,
        string? Description) : IRequest<CreateExamTypeResult>;
}
