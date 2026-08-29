using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.ExamConfig.Queries.GetPromptTemplatesByExamType
{
    public record PromptTemplateResultItem(
        Guid Id,
        Guid? ExamTypeId,
        SubjectCategory? SubjectCategory,
        AiOperationType TemplateType,
        TemplateLayer Layer,
        string TemplateContent,
        int Version,
        bool IsActive);
}
