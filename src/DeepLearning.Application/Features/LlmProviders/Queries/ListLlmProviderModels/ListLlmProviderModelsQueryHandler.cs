using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Queries.ListLlmProviderModels
{
    public class ListLlmProviderModelsQueryHandler : IRequestHandler<ListLlmProviderModelsQuery, List<LlmProviderModelResultItem>>
    {
        private readonly ILlmProviderSettingsRepository _providerRepository;
        private readonly ILlmProviderModelRepository _modelRepository;

        public ListLlmProviderModelsQueryHandler(
            ILlmProviderSettingsRepository providerRepository, ILlmProviderModelRepository modelRepository)
        {
            _providerRepository = providerRepository;
            _modelRepository = modelRepository;
        }

        public async Task<List<LlmProviderModelResultItem>> Handle(ListLlmProviderModelsQuery request, CancellationToken cancellationToken)
        {
            await _providerRepository.GetByProviderKeyAsync(request.ProviderKey, cancellationToken)
                .EnsureFoundAsync(nameof(Domain.Entities.LlmProviderSettings), request.ProviderKey);

            var models = await _modelRepository.ListByProviderKeyAsync(request.ProviderKey, cancellationToken);

            return models.Select(x => new LlmProviderModelResultItem(
                x.ProviderKey, x.Model, x.Label, x.IsCurrent, x.CreatedAt)).ToList();
        }
    }
}
