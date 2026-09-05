using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Queries.ListAiOperationOverrides
{
    public class ListAiOperationOverridesQueryHandler : IRequestHandler<ListAiOperationOverridesQuery, List<AiOperationOverrideResultItem>>
    {
        private readonly IAiOperationProviderOverrideRepository _overrideRepository;

        public ListAiOperationOverridesQueryHandler(IAiOperationProviderOverrideRepository overrideRepository)
        {
            _overrideRepository = overrideRepository;
        }

        public async Task<List<AiOperationOverrideResultItem>> Handle(
            ListAiOperationOverridesQuery request, CancellationToken cancellationToken)
        {
            var overrides = await _overrideRepository.ListAsync(cancellationToken);
            var overrideByOperationType = overrides.ToDictionary(x => x.OperationType);

            return Enum.GetValues<AiOperationType>()
                .Select(operationType => overrideByOperationType.TryGetValue(operationType, out var row)
                    ? new AiOperationOverrideResultItem(operationType, row.ProviderKey, row.Model, row.ThinkingEnabled, row.Effort, row.UpdatedAt)
                    : new AiOperationOverrideResultItem(operationType, null, null, null, null, null))
                .ToList();
        }
    }
}
