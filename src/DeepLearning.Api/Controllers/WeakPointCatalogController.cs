using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.WeakPointCatalogAdmin.Commands.CreateWeakPointCatalogEntry;
using DeepLearning.Application.Features.WeakPointCatalogAdmin.Commands.MergeWeakPointCatalog;
using DeepLearning.Application.Features.WeakPointCatalogAdmin.Commands.UpdateWeakPointCatalogEntry;
using DeepLearning.Application.Features.WeakPointCatalogAdmin.Queries.ListWeakPointCatalog;
using DeepLearning.Application.Features.WeakPointCatalogAdmin.Queries.ListWeakPointCategories;
using DeepLearning.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.WeakPointCatalog.Base)]
    public class WeakPointCatalogController : ControllerBase
    {
        private readonly IMediator _mediator;

        public WeakPointCatalogController(IMediator mediator)
        {
            _mediator = mediator;
        }

        public record CreateWeakPointCatalogRequest(
            Guid CategoryId, string Code, string Name, string Description, string? DefaultDimensionKey, string? DefaultErrorCategory);

        public record UpdateWeakPointCatalogRequest(
            string? Name, string? Description, string? DefaultDimensionKey, string? DefaultErrorCategory, WeakPointCatalogStatus? Status);

        public record MergeWeakPointCatalogRequest(Guid FromId, Guid ToId);

        [HttpGet]
        public async Task<ActionResult<List<WeakPointCatalogResultItem>>> List(
            WeakPointCatalogStatus? status, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListWeakPointCatalogQuery(status), cancellationToken));

        [HttpGet("categories")]
        public async Task<ActionResult<List<WeakPointCategoryResultItem>>> ListCategories(CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListWeakPointCategoriesQuery(), cancellationToken));

        [HttpPost]
        public async Task<ActionResult<CreateWeakPointCatalogEntryResult>> Create(
            CreateWeakPointCatalogRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CreateWeakPointCatalogEntryCommand(
                    request.CategoryId, request.Code, request.Name, request.Description,
                    request.DefaultDimensionKey, request.DefaultErrorCategory),
                cancellationToken);

            return CreatedAtAction(nameof(List), result);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<UpdateWeakPointCatalogEntryResult>> Update(
            Guid id, UpdateWeakPointCatalogRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(
                new UpdateWeakPointCatalogEntryCommand(
                    id, request.Name, request.Description, request.DefaultDimensionKey, request.DefaultErrorCategory, request.Status),
                cancellationToken));

        [HttpPost("merge")]
        public async Task<ActionResult<MergeWeakPointCatalogResult>> Merge(
            MergeWeakPointCatalogRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new MergeWeakPointCatalogCommand(request.FromId, request.ToId), cancellationToken));
    }
}
