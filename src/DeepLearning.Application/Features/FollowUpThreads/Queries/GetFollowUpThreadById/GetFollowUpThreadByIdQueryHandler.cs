using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Queries.GetFollowUpThreadById
{
    public class GetFollowUpThreadByIdQueryHandler : IRequestHandler<GetFollowUpThreadByIdQuery, FollowUpThreadResult>
    {
        private readonly IFollowUpThreadRepository _followUpThreadRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IStandardOverrideRepository _standardOverrideRepository;

        public GetFollowUpThreadByIdQueryHandler(
            IFollowUpThreadRepository followUpThreadRepository,
            ISubmissionRepository submissionRepository,
            IStandardOverrideRepository standardOverrideRepository)
        {
            _followUpThreadRepository = followUpThreadRepository;
            _submissionRepository = submissionRepository;
            _standardOverrideRepository = standardOverrideRepository;
        }

        public async Task<FollowUpThreadResult> Handle(GetFollowUpThreadByIdQuery request, CancellationToken cancellationToken)
        {
            var thread = await _followUpThreadRepository.GetByIdWithMessagesAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(FollowUpThread), request.Id);

            var submission = await _submissionRepository.GetByIdAsync(thread.SubmissionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Submission), thread.SubmissionId);

            var standardOverride = thread.StandardOverrideId is { } overrideId
                ? await _standardOverrideRepository.GetByIdAsync(overrideId, cancellationToken)
                : null;

            return FollowUpThreadResult.From(thread, submission.Status, standardOverride?.Status);
        }
    }
}
