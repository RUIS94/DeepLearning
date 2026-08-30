using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface ISeedReferenceLinkRepository
    {
        Task<List<SeedReferenceLink>> ListByGeneratedQuestionIdAsync(Guid generatedQuestionId, CancellationToken cancellationToken = default);

        Task AddRangeAsync(IEnumerable<SeedReferenceLink> links, CancellationToken cancellationToken = default);
    }
}
