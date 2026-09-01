using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Submissions.Commands.CreateSubmission;
using DeepLearning.Application.Features.Submissions.Commands.GradeSubmission;
using DeepLearning.Application.Features.Submissions.Queries.GetSubmissionById;
using DeepLearning.Application.Features.Submissions.Queries.ListSubmissions;
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

        [HttpPost("{id:guid}/grade")]
        public async Task<ActionResult<GradeSubmissionResult>> Grade(Guid id, GradeSubmissionRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GradeSubmissionCommand(id, request.ExamTypeId), cancellationToken));

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
