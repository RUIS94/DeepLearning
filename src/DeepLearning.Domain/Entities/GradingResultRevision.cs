using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    /// <summary>
    /// Audit row for one score_challenge re-grade: a FollowUpThreadKind.score_challenge thread
    /// closed with decision=adjust, so CloseFollowUpThreadCommandHandler rewrote the disputed
    /// dimension's <see cref="GradingResult.Band"/> in place and recorded the before/after here.
    /// The GradingResult itself is mutated (not versioned) so the rest of the app keeps reading
    /// one row per (submission, dimension); this table is the only place the original Band and
    /// the reason survive. Never written by any flow other than that close handler.
    /// </summary>
    public class GradingResultRevision : Entity
    {
        public Guid SubmissionId { get; set; }
        public Guid DimensionId { get; set; }
        public Guid GradingResultId { get; set; }

        /// <summary>The Band the dimension held before this re-grade.</summary>
        public int FromBand { get; set; }

        /// <summary>The Band it was set to.</summary>
        public int ToBand { get; set; }

        /// <summary>The summary call's revisedRationale — why the Band changed.</summary>
        public string Reason { get; set; } = string.Empty;

        public Guid TriggeredByFollowUpThreadId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public Submission? Submission { get; set; }
        public AssessmentDimension? Dimension { get; set; }
        public FollowUpThread? TriggeredByFollowUpThread { get; set; }
    }
}
