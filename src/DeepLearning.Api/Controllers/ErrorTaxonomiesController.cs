using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateErrorTaxonomy;
using DeepLearning.Application.Features.ExamConfig.Queries.GetErrorTaxonomiesByExamType;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.ErrorTaxonomies.Base)]
    public class ErrorTaxonomiesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ErrorTaxonomiesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        public record CreateErrorTaxonomyRequest(
            string CategoryKey, string CategoryName, string? Description, string? ExampleCases);

        [HttpPost]
        public async Task<ActionResult<CreateErrorTaxonomyResult>> Create(
            Guid examTypeId, CreateErrorTaxonomyRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CreateErrorTaxonomyCommand(examTypeId, request.CategoryKey, request.CategoryName, request.Description, request.ExampleCases),
                cancellationToken);

            return CreatedAtAction(nameof(List), new { examTypeId }, result);
        }

        [HttpGet]
        public async Task<ActionResult<List<ErrorTaxonomyResultItem>>> List(Guid examTypeId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetErrorTaxonomiesByExamTypeQuery(examTypeId), cancellationToken));
    }
}
