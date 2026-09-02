using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IFollowUpThreadRepository
    {
        /// <summary>Messages ordered by CreatedAt ascending (oldest first — conversation order).</summary>
        Task<FollowUpThread?> GetByIdWithMessagesAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>All threads for a submission, newest first, each with its messages (oldest first). Used for the thread list.</summary>
        Task<List<FollowUpThread>> ListBySubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default);

        /// <summary>True if the submission has a thread whose status is still open — at most one is ever allowed.</summary>
        Task<bool> HasOpenThreadForSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default);

        Task AddAsync(FollowUpThread thread, CancellationToken cancellationToken = default);

        Task AddMessageAsync(FollowUpMessage message, CancellationToken cancellationToken = default);
    }
}
