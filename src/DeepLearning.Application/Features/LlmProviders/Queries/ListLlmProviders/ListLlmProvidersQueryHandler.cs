using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Queries.ListLlmProviders
{
    public class ListLlmProvidersQueryHandler : IRequestHandler<ListLlmProvidersQuery, List<LlmProviderResultItem>>
    {
        private readonly ILlmProviderSettingsRepository _repository;

        public ListLlmProvidersQueryHandler(ILlmProviderSettingsRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<LlmProviderResultItem>> Handle(ListLlmProvidersQuery request, CancellationToken cancellationToken)
        {
            var settings = await _repository.ListAsync(cancellationToken);

            return settings.Select(x => new LlmProviderResultItem(
                x.ProviderKey, x.IsActive, x.Model, x.ThinkingEnabled, x.Effort, x.ExtraSettings, x.UpdatedAt)).ToList();
        }
    }
}
