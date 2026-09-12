using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Queries.GetSeedReferenceLinksByQuestionId
{
    public class GetSeedReferenceLinksByQuestionIdQueryHandler
        : IRequestHandler<GetSeedReferenceLinksByQuestionIdQuery, List<SeedReferenceLinkResultItem>>
    {
        private readonly IQuestionRepository _questionRepository;
        private readonly ISeedReferenceLinkRepository _seedReferenceLinkRepository;

        public GetSeedReferenceLinksByQuestionIdQueryHandler(
            IQuestionRepository questionRepository,
            ISeedReferenceLinkRepository seedReferenceLinkRepository)
        {
            _questionRepository = questionRepository;
            _seedReferenceLinkRepository = seedReferenceLinkRepository;
        }

        public async Task<List<SeedReferenceLinkResultItem>> Handle(GetSeedReferenceLinksByQuestionIdQuery request, CancellationToken cancellationToken)
        {
            var question = await _questionRepository.GetByIdAsync(request.GeneratedQuestionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Question), request.GeneratedQuestionId);

            if (question.Visibility != Visibility.Shared && question.CreatedBy != request.RequesterId)
            {
                throw new NotFoundException(nameof(Question), request.GeneratedQuestionId);
            }

            var links = await _seedReferenceLinkRepository.ListByGeneratedQuestionIdAsync(request.GeneratedQuestionId, cancellationToken);

            return links.Select(x => new SeedReferenceLinkResultItem(
                x.Id, x.SeedQuestionId, x.SeedQuestion?.Title ?? string.Empty, x.SimilarityReason, x.CreatedAt)).ToList();
        }
    }
}
