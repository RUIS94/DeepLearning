using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.FollowUpThreads;
using DeepLearning.Application.Features.FollowUpThreads.Commands.AddFollowUpMessage;
using DeepLearning.Application.Features.FollowUpThreads.Commands.CloseFollowUpThread;
using DeepLearning.Application.Features.FollowUpThreads.Commands.CreateFollowUpThread;
using DeepLearning.Application.Features.FollowUpThreads.Queries.GetFollowUpThreadBySubmissionId;
using DeepLearning.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.FollowUpThreads.Base)]
    public class FollowUpThreadsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUser;

        public FollowUpThreadsController(IMediator mediator, ICurrentUserService currentUser)
        {
            _mediator = mediator;
            _currentUser = currentUser;
        }

        public record CreateFollowUpThreadRequest(Guid SubmissionId, Guid UserId, Guid ExamTypeId, string? ContextRef, string QuestionText);

        [HttpPost]
        public async Task<ActionResult<FollowUpThreadResult>> Create(CreateFollowUpThreadRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? request.UserId;
            var result = await _mediator.Send(
                new CreateFollowUpThreadCommand(request.SubmissionId, userId, request.ExamTypeId, request.ContextRef, request.QuestionText),
                cancellationToken);

            return CreatedAtAction(nameof(GetBySubmission), new { submissionId = result.SubmissionId }, result);
        }

        public record AddFollowUpMessageRequest(Guid UserId, string QuestionText);

        [HttpPost("{id:guid}/messages")]
        public async Task<ActionResult<FollowUpThreadResult>> AddMessage(Guid id, AddFollowUpMessageRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? request.UserId;
            var result = await _mediator.Send(new AddFollowUpMessageCommand(id, userId, request.QuestionText), cancellationToken);
            return Ok(result);
        }

        public record CloseFollowUpThreadRequest(Guid UserId);

        [HttpPost("{id:guid}/close")]
        public async Task<ActionResult<FollowUpThreadResult>> Close(Guid id, CloseFollowUpThreadRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? request.UserId;
            var result = await _mediator.Send(new CloseFollowUpThreadCommand(id, userId), cancellationToken);
            return Ok(result);
        }

        [HttpGet("by-submission/{submissionId:guid}")]
        public async Task<ActionResult<FollowUpThreadResult>> GetBySubmission(Guid submissionId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetFollowUpThreadBySubmissionIdQuery(submissionId), cancellationToken));
    }
}
