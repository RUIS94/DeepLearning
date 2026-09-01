using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IFollowUpThreadRepository
    {
        /// <summary>Messages ordered by CreatedAt ascending (oldest first — conversation order).</summary>
        Task<FollowUpThread?> GetByIdWithMessagesAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Messages ordered by CreatedAt ascending. At most one thread per submission — see FollowUpThread's doc comment.</summary>
        Task<FollowUpThread?> GetBySubmissionIdWithMessagesAsync(Guid submissionId, CancellationToken cancellationToken = default);

        Task<bool> ExistsForSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default);

        Task AddAsync(FollowUpThread thread, CancellationToken cancellationToken = default);

        Task AddMessageAsync(FollowUpMessage message, CancellationToken cancellationToken = default);
    }
}
