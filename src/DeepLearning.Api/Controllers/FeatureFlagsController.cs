using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.FeatureFlags.Commands.SetFeatureFlag;
using DeepLearning.Application.Features.FeatureFlags.Queries.ListFeatureFlags;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    /// <summary>
    /// Read/toggle the <c>feature_flags</c> the code consults (题库 / 复习库, design doc §六).
    /// Backs the settings screen's "功能开关" section. No role gate — same "trust the caller"
    /// convention as the rest of the admin surface.
    /// </summary>
    [ApiController]
    [Route(ApiRoutes.FeatureFlags.Base)]
    public class FeatureFlagsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public FeatureFlagsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<List<FeatureFlagResultItem>>> List(CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListFeatureFlagsQuery(), cancellationToken));

        public record SetFeatureFlagRequest(bool Enabled);

        [HttpPut("{key}")]
        public async Task<ActionResult<SetFeatureFlagResult>> Set(
            string key, SetFeatureFlagRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new SetFeatureFlagCommand(key, request.Enabled), cancellationToken));
    }
}
