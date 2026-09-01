using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.ExamConfig.Commands.UpdatePromptTemplate
{
    public record UpdatePromptTemplateResult(
        Guid Id,
        Guid? ExamTypeId,
        SubjectCategory? SubjectCategory,
        AiOperationType TemplateType,
        TemplateLayer Layer,
        string TemplateContent,
        int Version,
        bool IsActive);
}
