using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.FollowUps.Commands.CreateFollowUpQuestion;
using DeepLearning.Application.Features.FollowUps.Queries.GetFollowUpQuestionById;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.FollowUps.Base)]
    public class FollowUpsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public FollowUpsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        public record CreateFollowUpQuestionRequest(Guid SubmissionId, Guid UserId, Guid ExamTypeId, string? ContextRef, string QuestionText);

        [HttpPost]
        public async Task<ActionResult<CreateFollowUpQuestionResult>> Create(CreateFollowUpQuestionRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CreateFollowUpQuestionCommand(request.SubmissionId, request.UserId, request.ExamTypeId, request.ContextRef, request.QuestionText),
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GetFollowUpQuestionByIdResult>> GetById(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetFollowUpQuestionByIdQuery(id), cancellationToken));
    }
}
