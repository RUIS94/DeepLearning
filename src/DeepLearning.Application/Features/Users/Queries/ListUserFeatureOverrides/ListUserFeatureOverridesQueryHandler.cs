using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.Users.Queries.ListUserFeatureOverrides
{
    public class ListUserFeatureOverridesQueryHandler
        : IRequestHandler<ListUserFeatureOverridesQuery, List<ListUserFeatureOverridesResultItem>>
    {
        private readonly IUserFeatureOverrideRepository _overrideRepository;

        public ListUserFeatureOverridesQueryHandler(IUserFeatureOverrideRepository overrideRepository)
        {
            _overrideRepository = overrideRepository;
        }

        public async Task<List<ListUserFeatureOverridesResultItem>> Handle(
            ListUserFeatureOverridesQuery request, CancellationToken cancellationToken)
        {
            var overrides = await _overrideRepository.ListByUserAsync(request.UserId, cancellationToken);
            return overrides.Select(o => new ListUserFeatureOverridesResultItem(o.FeatureKey, o.Enabled)).ToList();
        }
    }
}
