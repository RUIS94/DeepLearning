using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// Submission is the aggregate root; GradingResult/ErrorListItem are FK-only child rows
    /// fetched via separate methods, no EF collection nav properties — same convention already
    /// established for Question/MeaningCheckpoint/TaskBSeededError (see AGENTS.md).
    /// </summary>
    public interface ISubmissionRepository
    {
        Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// A user's submissions, newest first, optionally scoped to one question. Backs both the
        /// "打开做过的记录" list (GET /submissions?userId=&amp;questionId=) and the per-question
        /// attempt-count badge on the question bank list (grouped in the handler).
        /// </summary>
        Task<List<Submission>> ListByUserAsync(Guid userId, Guid? questionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// The <c>created_at</c> of the user's most recent <paramref name="count"/> graded
        /// submissions, newest first. Projection-only — backs UpdateWeakPointsOnGraded's
        /// "unseen in the last N graded submissions" resolve window without loading the whole
        /// submission history.
        /// </summary>
        Task<List<DateTimeOffset>> ListRecentGradedCreatedAtAsync(Guid userId, int count, CancellationToken cancellationToken = default);

        Task<List<GradingResult>> GetGradingResultsAsync(Guid submissionId, CancellationToken cancellationToken = default);

        Task<List<ErrorListItem>> GetErrorListAsync(Guid submissionId, CancellationToken cancellationToken = default);

        Task AddAsync(Submission submission, CancellationToken cancellationToken = default);

        Task AddGradingResultsAsync(IEnumerable<GradingResult> results, CancellationToken cancellationToken = default);

        Task AddErrorListItemsAsync(IEnumerable<ErrorListItem> items, CancellationToken cancellationToken = default);
    }
}
