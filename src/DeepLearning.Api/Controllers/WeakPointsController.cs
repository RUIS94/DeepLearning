using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.WeakPoints.Commands.ReclassifyWeakPoint;
using DeepLearning.Application.Features.WeakPoints.Queries.ListWeakPoints;
using DeepLearning.Application.Interfaces;
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
        private readonly ICurrentUserService _currentUser;

        public WeakPointsController(IMediator mediator, ICurrentUserService currentUser)
        {
            _mediator = mediator;
            _currentUser = currentUser;
        }

        public record ReclassifyRequest(Guid CatalogId);

        [HttpGet]
        public async Task<ActionResult<List<WeakPointResultItem>>> List(Guid? userId, WeakPointStatus? status, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListWeakPointsQuery(_currentUser.UserId ?? userId ?? Guid.Empty, status), cancellationToken));

        [HttpPost("{id:guid}/reclassify")]
        public async Task<ActionResult<ReclassifyWeakPointResult>> Reclassify(Guid id, ReclassifyRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ReclassifyWeakPointCommand(id, request.CatalogId), cancellationToken));
    }
}
