using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Queries.ListQuestions
{
    /// <summary>
    /// UserId is optional: when supplied, each result item carries this user's attempt count and
    /// latest submission id for that question (0 / null when the user hasn't attempted it).
    /// </summary>
    public record ListQuestionsQuery(
        TaskType? TaskType,
        Difficulty? Difficulty,
        bool? InBank,
        Guid? CategoryId,
        Guid? UserId = null,
        bool? IsSeedReference = null) : IRequest<List<ListQuestionsResultItem>>;
}
