using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Users.Commands.SetUserFeatureOverride;
using DeepLearning.Application.Features.Users.Commands.UpdateUserRole;
using DeepLearning.Application.Features.Users.Queries.ListUserFeatureOverrides;
using DeepLearning.Application.Features.Users.Queries.ListUsers;
using DeepLearning.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    /// <summary>
    /// User management (ref/管理员与用户权限隔离_策划书.md A3/A4) — admin-only end to end. This is
    /// a net-new capability: before this, nothing in the API could list every user or change a
    /// role; the only way in was the Admin:BootstrapEmails config's one-time seed (Phase 0).
    /// </summary>
    [ApiController]
    [Route(ApiRoutes.AdminUsers.Base)]
    [Authorize(Policy = "AdminOnly")]
    public class AdminUsersController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AdminUsersController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<ListUsersResult>> List(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
            => Ok(await _mediator.Send(new ListUsersQuery(page, pageSize), cancellationToken));

        public record UpdateUserRoleRequest(UserRole Role);

        [HttpPut("{id:guid}/role")]
        public async Task<ActionResult<UpdateUserRoleResult>> UpdateRole(
            Guid id, UpdateUserRoleRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new UpdateUserRoleCommand(id, request.Role), cancellationToken));

        [HttpGet("{id:guid}/features")]
        public async Task<ActionResult<List<ListUserFeatureOverridesResultItem>>> ListFeatureOverrides(
            Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListUserFeatureOverridesQuery(id), cancellationToken));

        public record SetFeatureOverrideRequest(bool? Enabled);

        /// <summary>Enabled=null clears the override, falling back to the global feature_flags value.</summary>
        [HttpPut("{id:guid}/features/{key}")]
        public async Task<ActionResult<SetUserFeatureOverrideResult>> SetFeatureOverride(
            Guid id, string key, SetFeatureOverrideRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new SetUserFeatureOverrideCommand(id, key, request.Enabled), cancellationToken));
    }
}
