using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// Design doc §10.6: standard_overrides is insert-only, forming an audit chain via
    /// PreviousOverrideId. A row is NOT a rewrite of the official rubric text
    /// (assessment_dimensions.level_descriptions stays authoritative and untouched) — it's a
    /// correction note patching how the AI applies the standard in a recurring situation (the AI
    /// misread the source text, or missed an error actually present in the user's translation).
    /// GetActiveByRuleAsync finds the currently-active correction (if any) that a new observing
    /// one for the same (scope, dimensionOrRule) would supersede if promoted.
    /// CountDistinctQuestionsPendingAsync backs the "confirmed independently on N different
    /// questions" activation threshold — it counts distinct Question ids (via
    /// FollowUpQuestion.SubmissionId -> Submission.QuestionId) among observing rows that share
    /// the same (scope, dimensionOrRule) and the same baselineOverrideId (the active row, if
    /// any, they'd each supersede) — grouping by baseline so a correction already promoted
    /// starts a fresh confirmation count for its own eventual successor.
    /// </summary>
    public interface IStandardOverrideRepository
    {
        Task<StandardOverride?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<StandardOverride?> GetActiveByRuleAsync(OverrideScope scope, string dimensionOrRule, CancellationToken cancellationToken = default);

        Task<int> CountDistinctQuestionsPendingAsync(
            OverrideScope scope,
            string dimensionOrRule,
            Guid? baselineOverrideId,
            CancellationToken cancellationToken = default);

        Task<List<StandardOverride>> ListAsync(OverrideStatus? status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Active correction patches that apply to a given exam type — <c>status = active</c> and
        /// (<c>exam_type_id = examTypeId</c> OR <c>exam_type_id IS NULL</c>, the latter being
        /// legacy/global rows written before the column existed). This is what
        /// GradeSubmissionCommandHandler feeds back into the grading prompt so a confirmed
        /// misjudgement isn't repeated.
        /// </summary>
        Task<List<StandardOverride>> ListActiveByExamTypeAsync(Guid examTypeId, CancellationToken cancellationToken = default);

        Task AddAsync(StandardOverride standardOverride, CancellationToken cancellationToken = default);
    }
}
