using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Users.Queries.GetUserById;
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

        public UsersController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GetUserByIdResult>> GetById(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetUserByIdQuery(id), cancellationToken));
    }
}
