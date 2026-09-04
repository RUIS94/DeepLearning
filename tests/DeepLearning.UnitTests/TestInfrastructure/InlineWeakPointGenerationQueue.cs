using DeepLearning.Application.Features.WeakPoints.Commands.GenerateWeakPoints;
using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.UnitTests.TestInfrastructure
{
    /// <summary>
    /// Runs weak-point extraction immediately instead of queueing it, so an API test can assert
    /// on the weak points a grading produced without waiting on a background worker.
    ///
    /// <para>Note this bypasses GenerateWeakPointsJob, and with it the status bookkeeping — the
    /// job's own transitions are covered separately in GenerateWeakPointsJobTests.</para>
    /// </summary>
    public class InlineWeakPointGenerationQueue : IWeakPointGenerationQueue
    {
        private readonly IMediator _mediator;

        public InlineWeakPointGenerationQueue(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task EnqueueAsync(
            Guid submissionId, Guid userId, Guid examTypeId, CancellationToken cancellationToken = default)
            => await _mediator.Send(new GenerateWeakPointsCommand(submissionId, userId, examTypeId), cancellationToken);
    }
}
