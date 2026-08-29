using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.LlmProviders.Commands.ActivateLlmProvider;
using DeepLearning.Application.Features.LlmProviders.Commands.UpdateLlmProviderSettings;
using DeepLearning.Application.Features.LlmProviders.Queries.ListLlmProviders;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    /// <summary>
    /// Admin surface over llm_provider_settings — which provider/model is active, per-provider
    /// thinking/effort/extra_settings. See AGENTS.md's "AI integration" section for the full
    /// design (why the table itself is hand-run SQL rather than an auto-applied migration).
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
            string? Model,
            bool? ThinkingEnabled,
            string? Effort,
            string? ExtraSettingsJson);

        [HttpPatch("{providerKey}")]
        public async Task<ActionResult<UpdateLlmProviderSettingsResult>> Update(
            string providerKey, UpdateLlmProviderSettingsRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new UpdateLlmProviderSettingsCommand(
                    providerKey, request.Model, request.ThinkingEnabled, request.Effort, request.ExtraSettingsJson),
                cancellationToken);

            return Ok(result);
        }

        [HttpPost("{providerKey}/activate")]
        public async Task<ActionResult<ActivateLlmProviderResult>> Activate(string providerKey, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ActivateLlmProviderCommand(providerKey), cancellationToken));
    }
}
