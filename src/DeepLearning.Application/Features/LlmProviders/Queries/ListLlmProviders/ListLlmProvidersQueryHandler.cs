using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Queries.ListLlmProviders
{
    public class ListLlmProvidersQueryHandler : IRequestHandler<ListLlmProvidersQuery, List<LlmProviderResultItem>>
    {
        private readonly ILlmProviderSettingsRepository _settingsRepository;
        private readonly ILlmProviderModelRepository _modelRepository;

        public ListLlmProvidersQueryHandler(ILlmProviderSettingsRepository settingsRepository, ILlmProviderModelRepository modelRepository)
        {
            _settingsRepository = settingsRepository;
            _modelRepository = modelRepository;
        }

        public async Task<List<LlmProviderResultItem>> Handle(ListLlmProvidersQuery request, CancellationToken cancellationToken)
        {
            var settings = await _settingsRepository.ListAsync(cancellationToken);
            var currentModels = await _modelRepository.ListCurrentAsync(cancellationToken);
            var currentModelByProvider = currentModels.ToDictionary(x => x.ProviderKey, x => x.Model);

            return settings.Select(x => new LlmProviderResultItem(
                x.ProviderKey,
                x.IsActive,
                currentModelByProvider.GetValueOrDefault(x.ProviderKey),
                x.ThinkingEnabled,
                x.Effort,
                x.ExtraSettings,
                x.UpdatedAt)).ToList();
        }
    }
}
