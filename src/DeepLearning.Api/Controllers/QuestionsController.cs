using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Questions.Commands.GenerateDeepLearningContent;
using DeepLearning.Application.Features.Questions.Commands.GenerateQuestion;
using DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion;
using DeepLearning.Application.Features.Questions.Queries.GetDeepLearningContentByQuestionId;
using DeepLearning.Application.Features.Questions.Queries.GetQuestionById;
using DeepLearning.Application.Features.Questions.Queries.GetSeedReferenceLinksByQuestionId;
using DeepLearning.Application.Features.Questions.Queries.ListQuestions;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.Questions.Base)]
    public class QuestionsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUser;

        public QuestionsController(IMediator mediator, ICurrentUserService currentUser)
        {
            _mediator = mediator;
            _currentUser = currentUser;
        }

        public record ImportUserQuestionRequest(
            TaskType TaskType,
            Difficulty Difficulty,
            string Title,
            string? Brief,
            string SourceText,
            string? FlawedTranslationText,
            Guid? CreatedBy,
            Visibility Visibility,
            List<MeaningCheckpointInput> MeaningCheckpoints,
            List<SeededErrorInput> SeededErrors,
            bool IsSeedReference = false);

        [HttpPost]
        public async Task<ActionResult<ImportUserQuestionResult>> Import(ImportUserQuestionRequest request, CancellationToken cancellationToken)
        {
            var createdBy = _currentUser.UserId ?? request.CreatedBy;
            var result = await _mediator.Send(
                new ImportUserQuestionCommand(
                    request.TaskType, request.Difficulty, request.Title, request.Brief, request.SourceText,
                    request.FlawedTranslationText, createdBy, request.Visibility,
                    request.MeaningCheckpoints, request.SeededErrors, request.IsSeedReference),
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        public record GenerateQuestionRequest(
            Guid ExamTypeId,
            TaskType TaskType,
            Difficulty? Difficulty,
            Guid? CategoryId,
            List<Guid>? SeedQuestionIds,
            Guid? CreatedBy,
            bool TargetWeakPoints = false);

        [HttpPost("generate")]
        public async Task<ActionResult<GenerateQuestionResult>> Generate(GenerateQuestionRequest request, CancellationToken cancellationToken)
        {
            var createdBy = _currentUser.UserId ?? request.CreatedBy;
            var result = await _mediator.Send(
                new GenerateQuestionCommand(
                    request.ExamTypeId, request.TaskType, request.Difficulty, request.CategoryId, request.SeedQuestionIds,
                    createdBy, request.TargetWeakPoints),
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GetQuestionByIdResult>> GetById(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetQuestionByIdQuery(id), cancellationToken));

        [HttpGet]
        public async Task<ActionResult<List<ListQuestionsResultItem>>> List(
            TaskType? taskType, Difficulty? difficulty, bool? inBank, Guid? categoryId, Guid? userId,
            bool? isSeedReference, CancellationToken cancellationToken)
        {
            // JWT identity wins over an explicit ?userId= (same opt-in convention as the write endpoints).
            var effectiveUserId = _currentUser.UserId ?? userId;
            return Ok(await _mediator.Send(
                new ListQuestionsQuery(taskType, difficulty, inBank, categoryId, effectiveUserId, isSeedReference),
                cancellationToken));
        }

        // Design doc §11.2 Step 8: "记录了每次出题参考了哪些真题" traceability read.
        [HttpGet("{id:guid}/seed-references")]
        public async Task<ActionResult<List<SeedReferenceLinkResultItem>>> GetSeedReferences(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetSeedReferenceLinksByQuestionIdQuery(id), cancellationToken));

        public record GenerateDeepLearningContentRequest(Guid ExamTypeId);

        [HttpPost("{id:guid}/deep-learning")]
        public async Task<ActionResult<GenerateDeepLearningContentResult>> GenerateDeepLearningContent(
            Guid id, GenerateDeepLearningContentRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GenerateDeepLearningContentCommand(id, request.ExamTypeId), cancellationToken));

        [HttpGet("{id:guid}/deep-learning")]
        public async Task<ActionResult<GetDeepLearningContentByQuestionIdResult>> GetDeepLearningContent(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetDeepLearningContentByQuestionIdQuery(id), cancellationToken));
    }
}
