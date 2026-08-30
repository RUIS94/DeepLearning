using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ReviewLibrary.Commands.MarkPatternReviewed;
using DeepLearning.Application.Features.ReviewLibrary.Commands.MarkVocabReviewed;
using DeepLearning.Application.Features.ReviewLibrary.Queries.ListReviewPatterns;
using DeepLearning.Application.Features.ReviewLibrary.Queries.ListReviewVocab;
using DeepLearning.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.ReviewLibrary.Base)]
    public class ReviewLibraryController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ReviewLibraryController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("patterns")]
        public async Task<ActionResult<List<ReviewPatternResultItem>>> ListPatterns(
            Guid userId, string? domain, string? scenario, string? frequencyTag, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListReviewPatternsQuery(userId, domain, scenario, frequencyTag), cancellationToken));

        [HttpGet("vocab")]
        public async Task<ActionResult<List<ReviewVocabResultItem>>> ListVocab(
            Guid userId, string? domain, string? scenario, string? frequencyTag, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListReviewVocabQuery(userId, domain, scenario, frequencyTag), cancellationToken));

        public record MarkReviewedRequest(Guid UserId, MasteryLevel MasteryLevel);

        [HttpPost("patterns/{patternId:guid}/review")]
        public async Task<ActionResult<MarkPatternReviewedResult>> MarkPatternReviewed(
            Guid patternId, MarkReviewedRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new MarkPatternReviewedCommand(request.UserId, patternId, request.MasteryLevel), cancellationToken));

        [HttpPost("vocab/{vocabId:guid}/review")]
        public async Task<ActionResult<MarkVocabReviewedResult>> MarkVocabReviewed(
            Guid vocabId, MarkReviewedRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new MarkVocabReviewedCommand(request.UserId, vocabId, request.MasteryLevel), cancellationToken));
    }
}
