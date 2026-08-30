using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Events
{
    /// <summary>
    /// Raised by Submission when TransitionTo(graded) succeeds (design doc §5's BUS/BE4 note:
    /// grading publishes this once, downstream WeakPoints/Progress/ReviewLibrary handlers each
    /// subscribe independently — adding a fourth subscriber later needs no change here).
    /// Carries only ids; handlers re-fetch whatever detail they need via the existing
    /// repositories rather than the event trying to anticipate every subscriber's needs.
    /// </summary>
    public class SubmissionGradedEvent
    {
        public Guid SubmissionId { get; init; }
        public Guid UserId { get; init; }
        public Guid QuestionId { get; init; }
        public Guid ExamTypeId { get; init; }
        public TaskType TaskType { get; init; }
        public DateTimeOffset GradedAt { get; init; }
    }
}
