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

        Task<List<GradingResult>> GetGradingResultsAsync(Guid submissionId, CancellationToken cancellationToken = default);

        Task<List<ErrorListItem>> GetErrorListAsync(Guid submissionId, CancellationToken cancellationToken = default);

        Task AddAsync(Submission submission, CancellationToken cancellationToken = default);

        Task AddGradingResultsAsync(IEnumerable<GradingResult> results, CancellationToken cancellationToken = default);

        Task AddErrorListItemsAsync(IEnumerable<ErrorListItem> items, CancellationToken cancellationToken = default);
    }
}
