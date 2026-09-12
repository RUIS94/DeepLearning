using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.FeatureFlags.Commands.SetFeatureFlag;
using DeepLearning.Application.Features.FeatureFlags.Queries.ListFeatureFlags;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    /// <summary>
    /// Read/toggle the global <c>feature_flags</c> the code consults (题库 / 复习库, design doc
    /// §六). Admin-only end to end — per-user overrides (which features a specific user can
    /// access) live on <see cref="AdminUsersController"/> instead, alongside the rest of user
    /// management.
    /// </summary>
    [ApiController]
    [Route(ApiRoutes.FeatureFlags.Base)]
    [Authorize(Policy = "AdminOnly")]
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
