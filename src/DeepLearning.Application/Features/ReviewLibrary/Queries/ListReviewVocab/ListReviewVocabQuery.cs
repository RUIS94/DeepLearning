using MediatR;

namespace DeepLearning.Application.Features.ReviewLibrary.Queries.ListReviewVocab
{
    public record ListReviewVocabQuery(
        Guid UserId,
        string? Domain,
        string? Scenario,
        string? FrequencyTag) : IRequest<List<ReviewVocabResultItem>>;
}
