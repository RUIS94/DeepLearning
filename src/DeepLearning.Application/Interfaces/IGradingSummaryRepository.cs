using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IGradingSummaryRepository
    {
        Task<GradingSummary?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken cancellationToken = default);

        Task AddAsync(GradingSummary summary, CancellationToken cancellationToken = default);
    }
}
