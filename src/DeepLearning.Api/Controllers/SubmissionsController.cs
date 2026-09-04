using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Submissions.Commands.CreateSubmission;
using DeepLearning.Application.Features.Submissions.Commands.GradeSubmission;
using DeepLearning.Application.Features.Submissions.Queries.GetSubmissionById;
using DeepLearning.Application.Features.Submissions.Queries.ListSubmissions;
using DeepLearning.Application.Features.Submissions.Queries.WaitForGradingStatus;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.Submissions.Base)]
    public class SubmissionsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUser;

        public SubmissionsController(IMediator mediator, ICurrentUserService currentUser)
        {
            _mediator = mediator;
            _currentUser = currentUser;
        }

        public record CreateSubmissionRequest(Guid QuestionId, Guid UserId, TaskType TaskType, string Content);

        [HttpPost]
        public async Task<ActionResult<CreateSubmissionResult>> Create(CreateSubmissionRequest request, CancellationToken cancellationToken)
        {
            // A valid JWT's identity always wins over whatever UserId the caller put in the body —
            // see AGENTS.md's Auth section for why this is opt-in rather than [Authorize]-enforced.
            var userId = _currentUser.UserId ?? request.UserId;
            var result = await _mediator.Send(
                new CreateSubmissionCommand(request.QuestionId, userId, request.TaskType, request.Content),
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        public record GradeSubmissionRequest(Guid ExamTypeId);

        /// <summary>Accepted-and-queued acknowledgement. There is no result yet — poll GET /submissions/{id}.</summary>
        public record GradeSubmissionAccepted(Guid SubmissionId, string Status);

        /// <summary>
        /// Queues a grading run and returns 202 immediately.
        ///
        /// <para>It used to grade inline. Grading is four LLM calls and measured over five
        /// minutes end to end, so it outlived the HTTP request every time — the browser got a
        /// 500 while the server quietly finished and persisted a perfectly good result the user
        /// never saw. The client now polls GET /submissions/{id} until the status leaves
        /// Grading.</para>
        /// </summary>
        [HttpPost("{id:guid}/grade")]
        [ProducesResponseType(typeof(GradeSubmissionAccepted), StatusCodes.Status202Accepted)]
        public async Task<ActionResult<GradeSubmissionAccepted>> Grade(
            Guid id, GradeSubmissionRequest request, IGradingJobQueue gradingJobs, CancellationToken cancellationToken)
        {
            await gradingJobs.EnqueueAsync(id, request.ExamTypeId, cancellationToken);

            // "grading" is what the client should expect to see next, not necessarily what the
            // row says this instant — the worker sets it a moment from now. The poll loop keys
            // off the submission's real status, so a brief "submitted" in between is harmless.
            return Accepted(new GradeSubmissionAccepted(id, SubmissionStatus.grading.ToString()));
        }

        /// <summary>
        /// Long-poll for the end of a grading run. Returns as soon as the submission leaves an
        /// in-progress status, or after <paramref name="waitSeconds"/> (capped), whichever comes
        /// first — so the client learns within a couple of seconds of the work finishing while
        /// making roughly one request a minute, instead of trading one against the other.
        /// </summary>
        [HttpGet("{id:guid}/grading-status")]
        public async Task<ActionResult<WaitForGradingStatusResult>> GradingStatus(
            Guid id, int waitSeconds, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new WaitForGradingStatusQuery(id, waitSeconds), cancellationToken));

        /// <summary>
        /// Queues weak-point extraction again for an already-graded submission. Exists because a
        /// failure there is recorded on the submission rather than retried automatically — an
        /// extraction run costs an LLM call, so re-running it is the learner's call, not a
        /// silent loop.
        /// </summary>
        [HttpPost("{id:guid}/weak-points/regenerate")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        public async Task<ActionResult> RegenerateWeakPoints(
            Guid id, RegenerateWeakPointsRequest request, IWeakPointGenerationQueue queue, CancellationToken cancellationToken)
        {
            var submission = await _mediator.Send(new GetSubmissionByIdQuery(id), cancellationToken);
            if (submission.Status is not (SubmissionStatus.graded or SubmissionStatus.standard_revised or SubmissionStatus.under_dispute))
            {
                return Conflict(new { title = "Weak points can only be generated for a graded submission." });
            }

            await queue.EnqueueAsync(id, submission.UserId, request.ExamTypeId, cancellationToken);
            return Accepted();
        }

        public record RegenerateWeakPointsRequest(Guid ExamTypeId);

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GetSubmissionByIdResult>> GetById(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetSubmissionByIdQuery(id), cancellationToken));

        [HttpGet]
        public async Task<ActionResult<List<ListSubmissionsResultItem>>> List(
            Guid? userId, Guid? questionId, CancellationToken cancellationToken)
        {
            var effectiveUserId = _currentUser.UserId ?? userId ?? Guid.Empty;
            return Ok(await _mediator.Send(
                new ListSubmissionsQuery(effectiveUserId, questionId), cancellationToken));
        }
    }
}
