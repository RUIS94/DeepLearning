using MediatR;

namespace DeepLearning.Application.Features.WeakPoints.Commands.GenerateWeakPoints
{
    /// <summary>
    /// Extracts this submission's weak points. Carries only ids — the handler re-fetches whatever
    /// it needs, exactly as it did when this ran off SubmissionGradedEvent.
    ///
    /// <para>It is a command sent from a background job rather than a subscriber to that event
    /// because it makes its own LLM call: leaving it on the grading path meant the learner waited
    /// on work whose result they were not being shown, and — worse — a failure inside it
    /// propagated out of the grading save and flipped a perfectly good grading to
    /// GradingFailed.</para>
    /// </summary>
    public record GenerateWeakPointsCommand(Guid SubmissionId, Guid UserId, Guid ExamTypeId) : IRequest;
}
