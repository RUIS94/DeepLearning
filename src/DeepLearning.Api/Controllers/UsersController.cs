using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Users.Commands.UpdateUserLanguagePreference;
using DeepLearning.Application.Features.Users.Queries.GetUserById;
using DeepLearning.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    /// <summary>
    /// Registration and login happen entirely against Supabase Auth now, not this backend — see
    /// AGENTS.md's Auth section. EnsureUserProfileMiddleware syncs a public.users row from a
    /// validated JWT the first time it's seen; there is deliberately no POST here to create one.
    /// </summary>
    [ApiController]
    [Route(ApiRoutes.Users.Base)]
    public class UsersController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUser;

        public UsersController(IMediator mediator, ICurrentUserService currentUser)
        {
            _mediator = mediator;
            _currentUser = currentUser;
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GetUserByIdResult>> GetById(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetUserByIdQuery(id), cancellationToken));

        public record UpdateLanguagePreferenceRequest(string LanguagePreference);

        /// <summary>
        /// Front-end UI language for the calling user ("en" or "zh"). Like the rest of this API,
        /// auth is opt-in: when a valid JWT is present its "sub" wins, otherwise the {id} in the
        /// route is trusted (see AGENTS.md's Auth section).
        /// </summary>
        [HttpPut("{id:guid}/language-preference")]
        public async Task<ActionResult<UpdateUserLanguagePreferenceResult>> UpdateLanguagePreference(
            Guid id, UpdateLanguagePreferenceRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(
                new UpdateUserLanguagePreferenceCommand(_currentUser.UserId ?? id, request.LanguagePreference),
                cancellationToken));
    }
}
