using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;

namespace DeepLearning.Domain.Entities
{
    public class Submission : AggregateRoot
    {
        public Guid QuestionId { get; set; }
        public Guid UserId { get; set; }
        public TaskType TaskType { get; set; }
        public string Content { get; set; } = string.Empty;
        public SubmissionStatus Status { get; set; } = SubmissionStatus.draft;
        public DateTimeOffset? SubmittedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public Question? Question { get; set; }
        public User? User { get; set; }

        /// <summary>
        /// Design doc §4.1's submission/grading lifecycle state machine. GradingFailed→Grading
        /// is the retry path (re-calling grade on a submission whose previous attempt failed);
        /// Submitted→Grading only accepts the first attempt. Every other status pair is illegal.
        /// </summary>
        private static readonly Dictionary<SubmissionStatus, SubmissionStatus[]> AllowedTransitions = new()
        {
            [SubmissionStatus.draft] = [SubmissionStatus.submitted],
            [SubmissionStatus.submitted] = [SubmissionStatus.grading],
            [SubmissionStatus.grading] = [SubmissionStatus.grading_failed, SubmissionStatus.graded],
            [SubmissionStatus.grading_failed] = [SubmissionStatus.grading, SubmissionStatus.grading_abandoned],
            [SubmissionStatus.graded] = [SubmissionStatus.under_dispute, SubmissionStatus.archived],
            [SubmissionStatus.under_dispute] = [SubmissionStatus.standard_revised, SubmissionStatus.graded],
            [SubmissionStatus.standard_revised] = [SubmissionStatus.graded],
            [SubmissionStatus.grading_abandoned] = [],
            [SubmissionStatus.archived] = [],
        };

        public void TransitionTo(SubmissionStatus target)
        {
            if (!AllowedTransitions[Status].Contains(target))
            {
                throw new InvalidSubmissionStateException(Id, Status, target);
            }

            Status = target;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
