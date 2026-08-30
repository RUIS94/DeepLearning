using MediatR;

namespace DeepLearning.Application.Features.Questions.Commands.GenerateDeepLearningContent
{
    public record GenerateDeepLearningContentCommand(Guid QuestionId, Guid ExamTypeId) : IRequest<GenerateDeepLearningContentResult>;
}
