using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.ReviewLibrary.Queries.ListReviewPatterns
{
    public class ListReviewPatternsQueryHandler : IRequestHandler<ListReviewPatternsQuery, List<ReviewPatternResultItem>>
    {
        private readonly IReviewLibraryRepository _reviewLibraryRepository;

        public ListReviewPatternsQueryHandler(IReviewLibraryRepository reviewLibraryRepository)
        {
            _reviewLibraryRepository = reviewLibraryRepository;
        }

        public async Task<List<ReviewPatternResultItem>> Handle(ListReviewPatternsQuery request, CancellationToken cancellationToken)
        {
            var patterns = await _reviewLibraryRepository.ListPatternsAsync(request.Domain, request.Scenario, request.FrequencyTag, cancellationToken);
            var reviews = await _reviewLibraryRepository.ListUserPatternReviewsAsync(request.UserId, patterns.Select(p => p.Id), cancellationToken);
            var reviewsByPattern = reviews.ToDictionary(r => r.PatternId);

            return patterns.Select(p =>
            {
                reviewsByPattern.TryGetValue(p.Id, out var review);
                return new ReviewPatternResultItem(
                    p.Id, p.QuestionId, p.PatternName, p.ExampleSentence, p.Domain, p.Scenario, p.FrequencyTag,
                    review?.TimesEncountered ?? 0, review?.MasteryLevel ?? MasteryLevel.New, review?.LastReviewedAt);
            }).ToList();
        }
    }
}
