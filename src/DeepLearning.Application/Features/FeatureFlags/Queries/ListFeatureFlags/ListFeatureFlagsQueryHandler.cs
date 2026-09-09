using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.FeatureFlags.Queries.ListFeatureFlags
{
    public class ListFeatureFlagsQueryHandler
        : IRequestHandler<ListFeatureFlagsQuery, List<FeatureFlagResultItem>>
    {
        private readonly IFeatureFlagRepository _repository;

        public ListFeatureFlagsQueryHandler(IFeatureFlagRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<FeatureFlagResultItem>> Handle(
            ListFeatureFlagsQuery request, CancellationToken cancellationToken)
        {
            var rows = (await _repository.ListAsync(cancellationToken))
                .ToDictionary(x => x.Key, x => x);

            return Common.FeatureFlags.Defaults.Keys
                .Select(key => rows.TryGetValue(key, out var row)
                    ? new FeatureFlagResultItem(key, row.Enabled, row.Scope, HasRow: true)
                    : new FeatureFlagResultItem(key, Common.FeatureFlags.DefaultFor(key), "global", HasRow: false))
                .OrderBy(x => x.Key)
                .ToList();
        }
    }
}
