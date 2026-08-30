using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.WeakPoints.Queries.ListWeakPoints;
using DeepLearning.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.WeakPoints.Base)]
    public class WeakPointsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public WeakPointsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<List<WeakPointResultItem>>> List(Guid userId, WeakPointStatus? status, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListWeakPointsQuery(userId, status), cancellationToken));
    }
}
