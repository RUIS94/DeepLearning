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
        /// <summary>
        /// Progress of the weak-point extraction that follows a successful grading. Null until
        /// this submission is graded (and on rows graded before the field existed) — see
        /// <see cref="WeakPointGenerationStatus"/> for why null is not the same as pending.
        /// </summary>
        public WeakPointGenerationStatus? WeakPointGenerationStatus { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public Question? Question { get; set; }
        public User? User { get; set; }

        /// <summary>
        /// Whether this submission has a grading result worth showing (as opposed to still being
        /// drafted/graded/retried). Single source for "which statuses count as having a result" —
        /// previously duplicated across a query handler and 2 frontend components (代码复用扫描
        /// §7/R-W-11). Api's SubmissionsController.RegenerateWeakPoints has its own status guard
        /// that currently does NOT include <see cref="SubmissionStatus.regraded"/> here — that gap
        /// is tracked separately (a TODO on that controller action), not silently closed by this
        /// property.
        /// </summary>
        public bool IsResultVisible => Status is SubmissionStatus.graded or SubmissionStatus.regraded
            or SubmissionStatus.standard_revised or SubmissionStatus.under_dispute;

        /// <summary>Whether a grading attempt is currently in flight for this submission.</summary>
        public static bool IsGradingInProgress(SubmissionStatus status) =>
            status is SubmissionStatus.submitted or SubmissionStatus.grading;

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
            // regraded is the score_challenge outcome; standard_revised the standardRevision one.
            [SubmissionStatus.under_dispute] = [SubmissionStatus.standard_revised, SubmissionStatus.graded, SubmissionStatus.regraded],
            [SubmissionStatus.standard_revised] = [SubmissionStatus.graded],
            // A re-graded submission behaves like graded: it can be disputed again or archived.
            [SubmissionStatus.regraded] = [SubmissionStatus.under_dispute, SubmissionStatus.archived],
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
