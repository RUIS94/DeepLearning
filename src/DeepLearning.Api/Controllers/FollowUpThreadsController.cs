using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.FollowUpThreads;
using DeepLearning.Application.Features.FollowUpThreads.Commands.AddFollowUpMessage;
using DeepLearning.Application.Features.FollowUpThreads.Commands.CloseFollowUpThread;
using DeepLearning.Application.Features.FollowUpThreads.Commands.CreateFollowUpThread;
using DeepLearning.Application.Features.FollowUpThreads.Queries.GetFollowUpThreadById;
using DeepLearning.Application.Features.FollowUpThreads.Queries.ListFollowUpThreadsBySubmission;
using DeepLearning.Application.Features.FollowUpThreads.Queries.PreviewFollowUpClose;
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

        public record CreateFollowUpThreadRequest(
            Guid SubmissionId,
            Guid UserId,
            Guid ExamTypeId,
            string? ContextRef,
            string QuestionText,
            DeepLearning.Domain.Enums.FollowUpThreadKind? Kind = null,
            Guid? DimensionId = null);

        [HttpPost]
        public async Task<ActionResult<FollowUpThreadResult>> Create(CreateFollowUpThreadRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? request.UserId;
            var result = await _mediator.Send(
                new CreateFollowUpThreadCommand(
                    request.SubmissionId, userId, request.ExamTypeId, request.ContextRef, request.QuestionText, request.Kind, request.DimensionId),
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        public record AddFollowUpMessageRequest(Guid UserId, string QuestionText);

        [HttpPost("{id:guid}/messages")]
        public async Task<ActionResult<FollowUpThreadResult>> AddMessage(Guid id, AddFollowUpMessageRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? request.UserId;
            var result = await _mediator.Send(new AddFollowUpMessageCommand(id, userId, request.QuestionText), cancellationToken);
            return Ok(result);
        }

        public record PreviewFollowUpCloseRequest(Guid UserId);

        /// <summary>Draft the closing summary (AI call) without committing — thread stays open.</summary>
        [HttpPost("{id:guid}/close/preview")]
        public async Task<ActionResult<FollowUpClosePreview>> PreviewClose(Guid id, PreviewFollowUpCloseRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? request.UserId;
            var result = await _mediator.Send(new PreviewFollowUpCloseQuery(id, userId), cancellationToken);
            return Ok(result);
        }

        public record CloseFollowUpThreadRequest(Guid UserId, FollowUpCloseInput? Input = null, bool SkipSummary = false);

        [HttpPost("{id:guid}/close")]
        public async Task<ActionResult<FollowUpThreadResult>> Close(Guid id, CloseFollowUpThreadRequest request, CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId ?? request.UserId;
            var result = await _mediator.Send(new CloseFollowUpThreadCommand(id, userId, request.Input, request.SkipSummary), cancellationToken);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<FollowUpThreadResult>> GetById(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetFollowUpThreadByIdQuery(id), cancellationToken));

        [HttpGet]
        public async Task<ActionResult<List<FollowUpThreadSummary>>> List(Guid submissionId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListFollowUpThreadsBySubmissionQuery(submissionId), cancellationToken));
    }
}
