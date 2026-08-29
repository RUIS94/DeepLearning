using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Commands.GenerateQuestion
{
    public record GenerateQuestionCommand(
        Guid ExamTypeId,
        TaskType TaskType,
        Difficulty Difficulty,
        Guid? CreatedBy) : IRequest<GenerateQuestionResult>;
}
