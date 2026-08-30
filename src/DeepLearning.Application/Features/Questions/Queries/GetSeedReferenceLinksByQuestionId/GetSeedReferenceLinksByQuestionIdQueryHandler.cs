using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Queries.GetSeedReferenceLinksByQuestionId
{
    public class GetSeedReferenceLinksByQuestionIdQueryHandler
        : IRequestHandler<GetSeedReferenceLinksByQuestionIdQuery, List<SeedReferenceLinkResultItem>>
    {
        private readonly ISeedReferenceLinkRepository _seedReferenceLinkRepository;

        public GetSeedReferenceLinksByQuestionIdQueryHandler(ISeedReferenceLinkRepository seedReferenceLinkRepository)
        {
            _seedReferenceLinkRepository = seedReferenceLinkRepository;
        }

        public async Task<List<SeedReferenceLinkResultItem>> Handle(GetSeedReferenceLinksByQuestionIdQuery request, CancellationToken cancellationToken)
        {
            var links = await _seedReferenceLinkRepository.ListByGeneratedQuestionIdAsync(request.GeneratedQuestionId, cancellationToken);

            return links.Select(x => new SeedReferenceLinkResultItem(
                x.Id, x.SeedQuestionId, x.SeedQuestion?.Title ?? string.Empty, x.SimilarityReason, x.CreatedAt)).ToList();
        }
    }
}
