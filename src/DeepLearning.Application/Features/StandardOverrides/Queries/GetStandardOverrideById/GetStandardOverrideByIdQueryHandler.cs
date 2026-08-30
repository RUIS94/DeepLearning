using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.StandardOverrides.Queries.GetStandardOverrideById
{
    public class GetStandardOverrideByIdQueryHandler : IRequestHandler<GetStandardOverrideByIdQuery, GetStandardOverrideByIdResult>
    {
        private readonly IStandardOverrideRepository _standardOverrideRepository;

        public GetStandardOverrideByIdQueryHandler(IStandardOverrideRepository standardOverrideRepository)
        {
            _standardOverrideRepository = standardOverrideRepository;
        }

        public async Task<GetStandardOverrideByIdResult> Handle(GetStandardOverrideByIdQuery request, CancellationToken cancellationToken)
        {
            var entity = await _standardOverrideRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(StandardOverride), request.Id);

            return new GetStandardOverrideByIdResult(
                entity.Id,
                entity.Scope,
                entity.DimensionOrRule,
                entity.OriginalRuleText,
                entity.RevisedRuleText,
                entity.TriggeredByFollowupId,
                entity.Status,
                entity.PreviousOverrideId,
                entity.EffectiveFrom,
                entity.CreatedAt);
        }
    }
}
