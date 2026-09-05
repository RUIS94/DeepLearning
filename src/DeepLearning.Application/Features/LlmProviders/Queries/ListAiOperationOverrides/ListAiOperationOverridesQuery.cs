using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Queries.ListAiOperationOverrides
{
    public record ListAiOperationOverridesQuery : IRequest<List<AiOperationOverrideResultItem>>;
}
