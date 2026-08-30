using MediatR;

namespace DeepLearning.Application.Features.ReviewLibrary.Queries.ListReviewPatterns
{
    /// <summary>
    /// Design doc §2.2: cross-question browsing of accumulated sentence patterns, filtered by
    /// domain/scenario/frequency and overlaid with this user's own review progress.
    /// </summary>
    public record ListReviewPatternsQuery(
        Guid UserId,
        string? Domain,
        string? Scenario,
        string? FrequencyTag) : IRequest<List<ReviewPatternResultItem>>;
}
