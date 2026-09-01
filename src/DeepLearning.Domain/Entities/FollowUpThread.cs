using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    /// <summary>
    /// One dispute conversation for a submission — at most one per submission, ever (created
    /// lazily by the first CreateFollowUpThreadCommand, never reopened after it closes). Holds
    /// the submission in under_dispute for the thread's entire open lifetime (design decision,
    /// 2026-09-02: the user pushes back over several rounds while the AI's stance may itself
    /// shift, so "under dispute" is a real state of the submission for as long as that
    /// conversation is unresolved — not a per-message blip). Each round's AI reply
    /// (FollowUpMessage.Verdict) is purely conversational/informational; only
    /// CloseFollowUpThreadCommand's separate "summary" AI call (AiOperationType.followup_summary)
    /// decides FinalVerdict, whether a StandardOverride gets created, and where the submission
    /// ends up (Graded or StandardRevised -> Graded) — see that handler's doc comment.
    /// </summary>
    public class FollowUpThread : Entity
    {
        /// <summary>Round cap enforced by AddFollowUpMessageCommandHandler — 10 user/AI round pairs (20 messages) before the thread must be closed.</summary>
        public const int MaxRounds = 10;

        public Guid SubmissionId { get; set; }
        public Guid UserId { get; set; }
        public Guid ExamTypeId { get; set; }
        public string? ContextRef { get; set; }
        public FollowUpThreadStatus Status { get; set; } = FollowUpThreadStatus.open;
        public FollowUpVerdict? FinalVerdict { get; set; }
        public Guid? StandardOverrideId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ClosedAt { get; set; }

        public Submission? Submission { get; set; }
        public User? User { get; set; }
        public ExamType? ExamType { get; set; }
        public List<FollowUpMessage> Messages { get; set; } = [];
    }
}
