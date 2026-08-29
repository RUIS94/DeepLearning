using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.LlmProviders.Commands.ActivateLlmProvider;
using DeepLearning.Application.Features.LlmProviders.Commands.AddLlmProviderModel;
using DeepLearning.Application.Features.LlmProviders.Commands.SelectLlmProviderModel;
using DeepLearning.Application.Features.LlmProviders.Commands.UpdateLlmProviderSettings;
using DeepLearning.Application.Features.LlmProviders.Queries.ListLlmProviderModels;
using DeepLearning.Application.Features.LlmProviders.Queries.ListLlmProviders;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    /// <summary>
    /// Admin surface over llm_provider_settings (which provider is active, per-provider
    /// thinking/effort/extra_settings) and llm_provider_models (the catalog of known models per
    /// provider, and which one is current). See AGENTS.md's "AI integration" section for the
    /// full design (why both tables are hand-run SQL rather than an auto-applied migration).
    /// </summary>
    [ApiController]
    [Route(ApiRoutes.LlmProviderSettings.Base)]
    public class LlmProviderSettingsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public LlmProviderSettingsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<List<LlmProviderResultItem>>> List(CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListLlmProvidersQuery(), cancellationToken));

        public record UpdateLlmProviderSettingsRequest(
            bool? ThinkingEnabled,
            string? Effort,
            string? ExtraSettingsJson);

        [HttpPatch("{providerKey}")]
        public async Task<ActionResult<UpdateLlmProviderSettingsResult>> Update(
            string providerKey, UpdateLlmProviderSettingsRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new UpdateLlmProviderSettingsCommand(providerKey, request.ThinkingEnabled, request.Effort, request.ExtraSettingsJson),
                cancellationToken);

            return Ok(result);
        }

        [HttpPost("{providerKey}/activate")]
        public async Task<ActionResult<ActivateLlmProviderResult>> Activate(string providerKey, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ActivateLlmProviderCommand(providerKey), cancellationToken));

        [HttpGet("{providerKey}/models")]
        public async Task<ActionResult<List<LlmProviderModelResultItem>>> ListModels(string providerKey, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListLlmProviderModelsQuery(providerKey), cancellationToken));

        public record AddLlmProviderModelRequest(string Model, string? Label);

        [HttpPost("{providerKey}/models")]
        public async Task<ActionResult<AddLlmProviderModelResult>> AddModel(
            string providerKey, AddLlmProviderModelRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new AddLlmProviderModelCommand(providerKey, request.Model, request.Label),
                cancellationToken);

            return Ok(result);
        }

        [HttpPost("{providerKey}/models/{model}/select")]
        public async Task<ActionResult<SelectLlmProviderModelResult>> SelectModel(
            string providerKey, string model, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new SelectLlmProviderModelCommand(providerKey, model), cancellationToken));
    }
}
