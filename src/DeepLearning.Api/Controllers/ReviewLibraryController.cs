using DeepLearning.Api.Constants;
using DeepLearning.Api.Filters;
using DeepLearning.Application.Common;
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
    [FeatureGate(FeatureFlags.ReviewLibraryEnabled)]
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
            string? domain, string? scenario, string? frequencyTag, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListReviewPatternsQuery(_currentUser.RequiredUserId, domain, scenario, frequencyTag), cancellationToken));

        [HttpGet("vocab")]
        public async Task<ActionResult<List<ReviewVocabResultItem>>> ListVocab(
            string? domain, string? scenario, string? frequencyTag, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListReviewVocabQuery(_currentUser.RequiredUserId, domain, scenario, frequencyTag), cancellationToken));

        public record MarkReviewedRequest(MasteryLevel MasteryLevel);

        [HttpPost("patterns/{patternId:guid}/review")]
        public async Task<ActionResult<MarkPatternReviewedResult>> MarkPatternReviewed(
            Guid patternId, MarkReviewedRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new MarkPatternReviewedCommand(_currentUser.RequiredUserId, patternId, request.MasteryLevel), cancellationToken));

        [HttpPost("vocab/{vocabId:guid}/review")]
        public async Task<ActionResult<MarkVocabReviewedResult>> MarkVocabReviewed(
            Guid vocabId, MarkReviewedRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new MarkVocabReviewedCommand(_currentUser.RequiredUserId, vocabId, request.MasteryLevel), cancellationToken));
    }
}
