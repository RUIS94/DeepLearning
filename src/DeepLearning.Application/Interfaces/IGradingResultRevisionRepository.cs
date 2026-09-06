using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>Append-only audit of score_challenge re-grades — written only by CloseFollowUpThreadCommandHandler.</summary>
    public interface IGradingResultRevisionRepository
    {
        Task AddAsync(GradingResultRevision revision, CancellationToken cancellationToken = default);
    }
}
