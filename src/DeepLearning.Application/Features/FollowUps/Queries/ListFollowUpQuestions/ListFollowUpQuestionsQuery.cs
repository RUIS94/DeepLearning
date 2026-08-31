using MediatR;

namespace DeepLearning.Application.Features.FollowUps.Queries.ListFollowUpQuestions
{
    public record ListFollowUpQuestionsQuery(Guid SubmissionId) : IRequest<List<FollowUpQuestionResultItem>>;
}
