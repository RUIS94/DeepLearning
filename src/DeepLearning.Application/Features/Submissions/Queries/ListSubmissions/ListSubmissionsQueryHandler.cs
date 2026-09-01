using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.Submissions.Queries.ListSubmissions
{
    public class ListSubmissionsQueryHandler
        : IRequestHandler<ListSubmissionsQuery, List<ListSubmissionsResultItem>>
    {
        private readonly ISubmissionRepository _submissionRepository;

        public ListSubmissionsQueryHandler(ISubmissionRepository submissionRepository)
        {
            _submissionRepository = submissionRepository;
        }

        public async Task<List<ListSubmissionsResultItem>> Handle(
            ListSubmissionsQuery request, CancellationToken cancellationToken)
        {
            var submissions = await _submissionRepository.ListByUserAsync(
                request.UserId, request.QuestionId, cancellationToken);

            return submissions
                .Select(s => new ListSubmissionsResultItem(
                    s.Id, s.QuestionId, s.TaskType, s.Status, s.SubmittedAt, s.CreatedAt))
                .ToList();
        }
    }
}
