using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ReviewLibrary.Commands.MarkPatternReviewed;
using DeepLearning.Application.Features.ReviewLibrary.Commands.MarkVocabReviewed;
using DeepLearning.Application.Features.ReviewLibrary.Queries.ListReviewPatterns;
using DeepLearning.Application.Features.ReviewLibrary.Queries.ListReviewVocab;
using DeepLearning.Application.Interfaces;
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
        private readonly ICurrentUserService _currentUser;

        public ReviewLibraryController(IMediator mediator, ICurrentUserService currentUser)
        {
            _mediator = mediator;
            _currentUser = currentUser;
        }

        [HttpGet("patterns")]
        public async Task<ActionResult<List<ReviewPatternResultItem>>> ListPatterns(
            Guid? userId, string? domain, string? scenario, string? frequencyTag, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListReviewPatternsQuery(ResolveUserId(userId), domain, scenario, frequencyTag), cancellationToken));

        [HttpGet("vocab")]
        public async Task<ActionResult<List<ReviewVocabResultItem>>> ListVocab(
            Guid? userId, string? domain, string? scenario, string? frequencyTag, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListReviewVocabQuery(ResolveUserId(userId), domain, scenario, frequencyTag), cancellationToken));

        public record MarkReviewedRequest(Guid? UserId, MasteryLevel MasteryLevel);

        [HttpPost("patterns/{patternId:guid}/review")]
        public async Task<ActionResult<MarkPatternReviewedResult>> MarkPatternReviewed(
            Guid patternId, MarkReviewedRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new MarkPatternReviewedCommand(ResolveUserId(request.UserId), patternId, request.MasteryLevel), cancellationToken));

        [HttpPost("vocab/{vocabId:guid}/review")]
        public async Task<ActionResult<MarkVocabReviewedResult>> MarkVocabReviewed(
            Guid vocabId, MarkReviewedRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new MarkVocabReviewedCommand(ResolveUserId(request.UserId), vocabId, request.MasteryLevel), cancellationToken));

        // A valid JWT's identity always wins over whatever UserId the caller passed explicitly —
        // see AGENTS.md's Auth section for why this is opt-in rather than [Authorize]-enforced.
        // Falls through to Guid.Empty (not null) when neither is present, same as this action's
        // pre-auth behavior of requiring userId — a missing identity is a caller bug, not a valid
        // "browse for nobody" request.
        private Guid ResolveUserId(Guid? explicitUserId) => _currentUser.UserId ?? explicitUserId ?? Guid.Empty;
    }
}
