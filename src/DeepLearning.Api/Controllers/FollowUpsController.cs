using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.FollowUps.Commands.CreateFollowUpQuestion;
using DeepLearning.Application.Features.FollowUps.Queries.GetFollowUpQuestionById;
using DeepLearning.Application.Features.FollowUps.Queries.ListFollowUpQuestions;
using DeepLearning.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.FollowUps.Base)]
    public class FollowUpsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUser;

        public FollowUpsController(IMediator mediator, ICurrentUserService currentUser)
        {
            _mediator = mediator;
            _currentUser = currentUser;
        }

        public record CreateFollowUpQuestionRequest(Guid SubmissionId, Guid UserId, Guid ExamTypeId, string? ContextRef, string QuestionText);

        [HttpPost]
        public async Task<ActionResult<CreateFollowUpQuestionResult>> Create(CreateFollowUpQuestionRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? request.UserId;
            var result = await _mediator.Send(
                new CreateFollowUpQuestionCommand(request.SubmissionId, userId, request.ExamTypeId, request.ContextRef, request.QuestionText),
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GetFollowUpQuestionByIdResult>> GetById(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetFollowUpQuestionByIdQuery(id), cancellationToken));

        [HttpGet]
        public async Task<ActionResult<List<FollowUpQuestionResultItem>>> List(Guid submissionId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListFollowUpQuestionsQuery(submissionId), cancellationToken));
    }
}
