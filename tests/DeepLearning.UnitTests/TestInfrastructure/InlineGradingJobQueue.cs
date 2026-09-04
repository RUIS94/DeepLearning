using DeepLearning.Application.Features.Submissions.Commands.GradeSubmission;
using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.UnitTests.TestInfrastructure
{
    /// <summary>
    /// Runs a grading immediately instead of queueing it — see ApiWebApplicationFactory for why
    /// the API tests want that, and for the one behavioural difference it introduces.
    /// </summary>
    public class InlineGradingJobQueue : IGradingJobQueue
    {
        private readonly IMediator _mediator;

        public InlineGradingJobQueue(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task EnqueueAsync(Guid submissionId, Guid examTypeId, CancellationToken cancellationToken = default)
            => await _mediator.Send(new GradeSubmissionCommand(submissionId, examTypeId), cancellationToken);
    }
}
