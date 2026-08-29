using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Submissions.Commands.CreateSubmission;
using DeepLearning.Application.Features.Submissions.Commands.GradeSubmission;
using DeepLearning.Application.Features.Submissions.Queries.GetSubmissionById;
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

        public SubmissionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        public record CreateSubmissionRequest(Guid QuestionId, Guid UserId, TaskType TaskType, string Content);

        [HttpPost]
        public async Task<ActionResult<CreateSubmissionResult>> Create(CreateSubmissionRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CreateSubmissionCommand(request.QuestionId, request.UserId, request.TaskType, request.Content),
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
    }
}
