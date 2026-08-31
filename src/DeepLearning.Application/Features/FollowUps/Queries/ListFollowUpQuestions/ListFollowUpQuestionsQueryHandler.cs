using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.FollowUps.Queries.ListFollowUpQuestions
{
    public class ListFollowUpQuestionsQueryHandler : IRequestHandler<ListFollowUpQuestionsQuery, List<FollowUpQuestionResultItem>>
    {
        private readonly IFollowUpQuestionRepository _followUpQuestionRepository;

        public ListFollowUpQuestionsQueryHandler(IFollowUpQuestionRepository followUpQuestionRepository)
        {
            _followUpQuestionRepository = followUpQuestionRepository;
        }

        public async Task<List<FollowUpQuestionResultItem>> Handle(ListFollowUpQuestionsQuery request, CancellationToken cancellationToken)
        {
            var followUps = await _followUpQuestionRepository.ListBySubmissionAsync(request.SubmissionId, cancellationToken);

            return followUps.Select(x => new FollowUpQuestionResultItem(
                x.Id,
                x.SubmissionId,
                x.UserId,
                x.ContextRef,
                x.QuestionText,
                x.AiResponse,
                x.Verdict,
                x.CreatedAt)).ToList();
        }
    }
}
