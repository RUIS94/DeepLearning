using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Queries.GetFollowUpThreadBySubmissionId
{
    public class GetFollowUpThreadBySubmissionIdQueryHandler : IRequestHandler<GetFollowUpThreadBySubmissionIdQuery, FollowUpThreadResult>
    {
        private readonly IFollowUpThreadRepository _followUpThreadRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IStandardOverrideRepository _standardOverrideRepository;

        public GetFollowUpThreadBySubmissionIdQueryHandler(
            IFollowUpThreadRepository followUpThreadRepository,
            ISubmissionRepository submissionRepository,
            IStandardOverrideRepository standardOverrideRepository)
        {
            _followUpThreadRepository = followUpThreadRepository;
            _submissionRepository = submissionRepository;
            _standardOverrideRepository = standardOverrideRepository;
        }

        public async Task<FollowUpThreadResult> Handle(GetFollowUpThreadBySubmissionIdQuery request, CancellationToken cancellationToken)
        {
            var thread = await _followUpThreadRepository.GetBySubmissionIdWithMessagesAsync(request.SubmissionId, cancellationToken)
                ?? throw new NotFoundException(nameof(FollowUpThread), request.SubmissionId);

            var submission = await _submissionRepository.GetByIdAsync(thread.SubmissionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Submission), thread.SubmissionId);

            var standardOverride = thread.StandardOverrideId is { } overrideId
                ? await _standardOverrideRepository.GetByIdAsync(overrideId, cancellationToken)
                : null;

            return FollowUpThreadResult.From(thread, submission.Status, standardOverride?.Status);
        }
    }
}
