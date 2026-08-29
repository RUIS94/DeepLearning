using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion
{
    public record ImportUserQuestionResult(
        Guid Id,
        TaskType TaskType,
        Difficulty Difficulty,
        string Title,
        DateTimeOffset CreatedAt);
}
