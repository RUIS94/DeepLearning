using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Queries.ListFollowUpThreadsBySubmission
{
    public class ListFollowUpThreadsBySubmissionQueryHandler
        : IRequestHandler<ListFollowUpThreadsBySubmissionQuery, List<FollowUpThreadSummary>>
    {
        private readonly IFollowUpThreadRepository _followUpThreadRepository;
        private readonly ISubmissionRepository _submissionRepository;

        public ListFollowUpThreadsBySubmissionQueryHandler(
            IFollowUpThreadRepository followUpThreadRepository, ISubmissionRepository submissionRepository)
        {
            _followUpThreadRepository = followUpThreadRepository;
            _submissionRepository = submissionRepository;
        }

        public async Task<List<FollowUpThreadSummary>> Handle(ListFollowUpThreadsBySubmissionQuery request, CancellationToken cancellationToken)
        {
            var submission = await _submissionRepository.GetByIdAsync(request.SubmissionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Submission), request.SubmissionId);

            if (submission.UserId != request.RequesterId)
            {
                throw new NotFoundException(nameof(Submission), request.SubmissionId);
            }

            var threads = await _followUpThreadRepository.ListBySubmissionAsync(request.SubmissionId, cancellationToken);
            return threads.Select(FollowUpThreadSummary.From).ToList();
        }
    }
}
