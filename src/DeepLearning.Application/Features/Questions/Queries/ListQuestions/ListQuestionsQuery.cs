using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Queries.ListQuestions
{
    public record ListQuestionsQuery(
        TaskType? TaskType,
        Difficulty? Difficulty,
        bool? InBank,
        Guid? CategoryId) : IRequest<List<ListQuestionsResultItem>>;
}
