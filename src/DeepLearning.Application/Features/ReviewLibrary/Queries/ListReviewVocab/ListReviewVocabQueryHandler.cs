using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.ReviewLibrary.Queries.ListReviewVocab
{
    public class ListReviewVocabQueryHandler : IRequestHandler<ListReviewVocabQuery, List<ReviewVocabResultItem>>
    {
        private readonly IReviewLibraryRepository _reviewLibraryRepository;

        public ListReviewVocabQueryHandler(IReviewLibraryRepository reviewLibraryRepository)
        {
            _reviewLibraryRepository = reviewLibraryRepository;
        }

        public async Task<List<ReviewVocabResultItem>> Handle(ListReviewVocabQuery request, CancellationToken cancellationToken)
        {
            var glossary = await _reviewLibraryRepository.ListGlossaryAsync(request.Domain, request.Scenario, request.FrequencyTag, cancellationToken);
            var reviews = await _reviewLibraryRepository.ListUserVocabReviewsAsync(request.UserId, glossary.Select(v => v.Id), cancellationToken);
            var reviewsByVocab = reviews.ToDictionary(r => r.VocabId);

            return glossary.Select(v =>
            {
                reviewsByVocab.TryGetValue(v.Id, out var review);
                return new ReviewVocabResultItem(
                    v.Id, v.EnglishExpr, v.ChineseEquiv, v.AccumulatedSemantics, v.Category, v.Domain, v.Scenario, v.FrequencyTag,
                    v.SenseCount, v.OccurrenceCount,
                    review?.TimesEncountered ?? 0, review?.MasteryLevel ?? MasteryLevel.New, review?.LastReviewedAt);
            }).ToList();
        }
    }
}
