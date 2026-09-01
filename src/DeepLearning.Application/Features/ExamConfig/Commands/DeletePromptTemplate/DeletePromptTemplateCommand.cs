using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Commands.DeletePromptTemplate
{
    public record DeletePromptTemplateCommand(Guid Id) : IRequest;
}
