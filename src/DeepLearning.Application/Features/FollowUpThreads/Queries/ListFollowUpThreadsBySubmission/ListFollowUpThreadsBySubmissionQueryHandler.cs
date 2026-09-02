using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Queries.ListFollowUpThreadsBySubmission
{
    public class ListFollowUpThreadsBySubmissionQueryHandler
        : IRequestHandler<ListFollowUpThreadsBySubmissionQuery, List<FollowUpThreadSummary>>
    {
        private readonly IFollowUpThreadRepository _followUpThreadRepository;

        public ListFollowUpThreadsBySubmissionQueryHandler(IFollowUpThreadRepository followUpThreadRepository)
        {
            _followUpThreadRepository = followUpThreadRepository;
        }

        public async Task<List<FollowUpThreadSummary>> Handle(ListFollowUpThreadsBySubmissionQuery request, CancellationToken cancellationToken)
        {
            var threads = await _followUpThreadRepository.ListBySubmissionAsync(request.SubmissionId, cancellationToken);
            return threads.Select(FollowUpThreadSummary.From).ToList();
        }
    }
}
