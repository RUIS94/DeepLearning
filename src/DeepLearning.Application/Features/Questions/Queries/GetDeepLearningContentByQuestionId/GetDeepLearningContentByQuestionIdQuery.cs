using MediatR;

namespace DeepLearning.Application.Features.Questions.Queries.GetDeepLearningContentByQuestionId
{
    public record GetDeepLearningContentByQuestionIdQuery(Guid QuestionId, Guid RequesterId) : IRequest<GetDeepLearningContentByQuestionIdResult>;
}
