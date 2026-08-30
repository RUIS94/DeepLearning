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
            var vocab = await _reviewLibraryRepository.ListVocabAsync(request.Domain, request.Scenario, request.FrequencyTag, cancellationToken);
            var reviews = await _reviewLibraryRepository.ListUserVocabReviewsAsync(request.UserId, vocab.Select(v => v.Id), cancellationToken);
            var reviewsByVocab = reviews.ToDictionary(r => r.VocabId);

            return vocab.Select(v =>
            {
                reviewsByVocab.TryGetValue(v.Id, out var review);
                return new ReviewVocabResultItem(
                    v.Id, v.QuestionId, v.EnglishExpr, v.ChineseEquiv, v.Domain, v.Scenario, v.FrequencyTag,
                    review?.TimesEncountered ?? 0, review?.MasteryLevel ?? MasteryLevel.New, review?.LastReviewedAt);
            }).ToList();
        }
    }
}
