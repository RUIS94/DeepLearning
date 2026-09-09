using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Queries.GetPromptTemplatesByExamType
{
    public record GetPromptTemplatesByExamTypeQuery(
        Guid? ExamTypeId,
        SubjectCategory? SubjectCategory,
        AiOperationType? TemplateType,
        bool? IsActive = null,
        // The admin surface passes an exam type's id and wants its own rows PLUS the shared
        // (NULL exam_type_id) ones it inherits — runtime prompt assembly (ExamConfigLoader)
        // does not go through this query and is unaffected.
        bool IncludeGlobalScope = false) : IRequest<List<PromptTemplateResultItem>>;
}
