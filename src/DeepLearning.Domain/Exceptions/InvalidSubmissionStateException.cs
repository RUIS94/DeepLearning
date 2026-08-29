using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Exceptions
{
    /// <summary>
    /// Thrown by Submission.TransitionTo when the requested status change isn't a legal move
    /// in the state machine (design doc §4.1) — e.g. grading a submission that's already
    /// Graded, or grading one that's still Draft. A 409 (resource exists but isn't in the
    /// right state), not a 400/404 — see ConflictException's mapping in GlobalExceptionHandler.
    /// </summary>
    public class InvalidSubmissionStateException : ConflictException
    {
        public InvalidSubmissionStateException(Guid submissionId, SubmissionStatus currentStatus, SubmissionStatus attemptedStatus)
            : base($"Submission '{submissionId}' cannot transition from '{currentStatus}' to '{attemptedStatus}'.")
        {
        }
    }
}
