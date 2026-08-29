using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Commands.CreatePromptTemplate
{
    public record CreatePromptTemplateCommand(
        Guid? ExamTypeId,
        SubjectCategory? SubjectCategory,
        AiOperationType TemplateType,
        TemplateLayer Layer,
        string TemplateContent,
        int Version) : IRequest<CreatePromptTemplateResult>;
}
