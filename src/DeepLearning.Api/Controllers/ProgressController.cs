using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Progress.Queries.GetProgressSnapshots;
using DeepLearning.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.Progress.Base)]
    public class ProgressController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUser;

        public ProgressController(IMediator mediator, ICurrentUserService currentUser)
        {
            _mediator = mediator;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<ActionResult<List<ProgressSnapshotResultItem>>> List(
            Guid? userId, string? difficultyTier, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(
                new GetProgressSnapshotsQuery(_currentUser.UserId ?? userId ?? Guid.Empty, difficultyTier),
                cancellationToken));
    }
}
