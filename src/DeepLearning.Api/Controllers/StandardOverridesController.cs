using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.StandardOverrides.Commands.ActivateStandardOverride;
using DeepLearning.Application.Features.StandardOverrides.Commands.DeprecateStandardOverride;
using DeepLearning.Application.Features.StandardOverrides.Queries.GetStandardOverrideById;
using DeepLearning.Application.Features.StandardOverrides.Queries.ListStandardOverrides;
using DeepLearning.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.StandardOverrides.Base)]
    public class StandardOverridesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public StandardOverridesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GetStandardOverrideByIdResult>> GetById(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetStandardOverrideByIdQuery(id), cancellationToken));

        [HttpGet]
        public async Task<ActionResult<List<StandardOverrideResultItem>>> List(OverrideStatus? status, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListStandardOverridesQuery(status), cancellationToken));

        [HttpPost("{id:guid}/activate")]
        public async Task<ActionResult<ActivateStandardOverrideResult>> Activate(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ActivateStandardOverrideCommand(id), cancellationToken));

        [HttpPost("{id:guid}/deprecate")]
        public async Task<ActionResult<DeprecateStandardOverrideResult>> Deprecate(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new DeprecateStandardOverrideCommand(id), cancellationToken));
    }
}
