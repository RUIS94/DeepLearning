using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.Questions.Commands.GenerateQuestion
{
    public record GenerateQuestionResult(
        Guid Id,
        TaskType TaskType,
        Difficulty Difficulty,
        string Title,
        DateTimeOffset CreatedAt);
}
