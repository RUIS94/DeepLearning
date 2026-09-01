using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.FollowUps.Queries.GetFollowUpQuestionById;
using DeepLearning.Application.Features.FollowUps.Queries.ListFollowUpQuestions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    /// <summary>
    /// Read-only now — the single-shot POST /follow-ups (CreateFollowUpQuestionCommand) was
    /// retired in favor of FollowUpThreadsController's multi-round thread model (design
    /// decision, 2026-09-02). Kept for historical audit of rows created before that change;
    /// see FollowUpQuestionConfiguration's table, which is untouched.
    /// </summary>
    [ApiController]
    [Route(ApiRoutes.FollowUps.Base)]
    public class FollowUpsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public FollowUpsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GetFollowUpQuestionByIdResult>> GetById(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetFollowUpQuestionByIdQuery(id), cancellationToken));

        [HttpGet]
        public async Task<ActionResult<List<FollowUpQuestionResultItem>>> List(Guid submissionId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListFollowUpQuestionsQuery(submissionId), cancellationToken));
    }
}
