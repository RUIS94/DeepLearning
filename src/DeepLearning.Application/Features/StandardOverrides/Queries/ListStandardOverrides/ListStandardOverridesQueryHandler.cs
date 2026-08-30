using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.StandardOverrides.Queries.ListStandardOverrides
{
    public class ListStandardOverridesQueryHandler : IRequestHandler<ListStandardOverridesQuery, List<StandardOverrideResultItem>>
    {
        private readonly IStandardOverrideRepository _standardOverrideRepository;

        public ListStandardOverridesQueryHandler(IStandardOverrideRepository standardOverrideRepository)
        {
            _standardOverrideRepository = standardOverrideRepository;
        }

        public async Task<List<StandardOverrideResultItem>> Handle(ListStandardOverridesQuery request, CancellationToken cancellationToken)
        {
            var overrides = await _standardOverrideRepository.ListAsync(request.Status, cancellationToken);

            return overrides.Select(x => new StandardOverrideResultItem(
                x.Id,
                x.Scope,
                x.DimensionOrRule,
                x.RevisedRuleText,
                x.Status,
                x.PreviousOverrideId,
                x.EffectiveFrom,
                x.CreatedAt)).ToList();
        }
    }
}
