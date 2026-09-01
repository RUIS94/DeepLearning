using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Queries.GetPromptTemplatesByExamType
{
    public record GetPromptTemplatesByExamTypeQuery(
        Guid? ExamTypeId,
        SubjectCategory? SubjectCategory,
        AiOperationType? TemplateType,
        bool? IsActive = null) : IRequest<List<PromptTemplateResultItem>>;
}
