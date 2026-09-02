using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class StandardOverride : AggregateRoot
    {
        public OverrideScope Scope { get; set; }

        /// <summary>
        /// Which exam type this correction applies to. Nullable: rows written before this
        /// column existed are treated as global and still apply to every exam type
        /// (see IStandardOverrideRepository.ListActiveByExamTypeAsync).
        /// </summary>
        public Guid? ExamTypeId { get; set; }
        public string DimensionOrRule { get; set; } = string.Empty;
        public string? OriginalRuleText { get; set; }
        public string RevisedRuleText { get; set; } = string.Empty;
        public Guid? TriggeredByFollowupId { get; set; }
        /// <summary>
        /// The follow-up-thread equivalent of TriggeredByFollowupId, added alongside it rather
        /// than reusing it (schema migration discipline: additive only — see schema.sql's own
        /// "只做加法,不改类型/删列" note) since threads live in a different table than the legacy
        /// single-shot FollowUpQuestion. Set by CloseFollowUpThreadCommandHandler; mutually
        /// exclusive with TriggeredByFollowupId in practice (old rows populate one, new rows the
        /// other). IStandardOverrideRepository.CountDistinctQuestionsPendingAsync counts
        /// confirmations from both so the activation threshold doesn't forget history predating
        /// this column.
        /// </summary>
        public Guid? TriggeredByFollowUpThreadId { get; set; }
        public OverrideStatus Status { get; set; } = OverrideStatus.observing;
        public Guid? PreviousOverrideId { get; set; }
        public DateTimeOffset? EffectiveFrom { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public FollowUpQuestion? TriggeredByFollowup { get; set; }
        public FollowUpThread? TriggeredByFollowUpThread { get; set; }
        public StandardOverride? PreviousOverride { get; set; }
        public ExamType? ExamType { get; set; }
    }
}
