using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.ExamConfig.Commands.CreatePromptTemplate
{
    public record CreatePromptTemplateResult(
        Guid Id,
        AiOperationType TemplateType,
        TemplateLayer Layer,
        int Version,
        bool IsActive);
}
