using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Users.Commands.RegisterUser;
using DeepLearning.Application.Features.Users.Queries.GetUserById;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.Users.Base)]
    public class UsersController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UsersController(IMediator mediator)
        {
            _mediator = mediator;
        }

        public record RegisterUserRequest(string Username, string Email, string Password, string? DisplayName);

        [HttpPost]
        public async Task<ActionResult<RegisterUserResult>> Register(RegisterUserRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new RegisterUserCommand(request.Username, request.Email, request.Password, request.DisplayName),
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GetUserByIdResult>> GetById(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetUserByIdQuery(id), cancellationToken));
    }
}
