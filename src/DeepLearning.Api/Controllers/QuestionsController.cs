using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Questions.Commands.GenerateQuestion;
using DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion;
using DeepLearning.Application.Features.Questions.Queries.GetQuestionById;
using DeepLearning.Application.Features.Questions.Queries.ListQuestions;
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

        public QuestionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        public record ImportUserQuestionRequest(
            TaskType TaskType,
            Difficulty Difficulty,
            string Title,
            string? Brief,
            string SourceText,
            string? FlawedTranslationText,
            int? WordCount,
            Guid? CreatedBy,
            Visibility Visibility,
            List<MeaningCheckpointInput> MeaningCheckpoints,
            List<SeededErrorInput> SeededErrors);

        [HttpPost]
        public async Task<ActionResult<ImportUserQuestionResult>> Import(ImportUserQuestionRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new ImportUserQuestionCommand(
                    request.TaskType, request.Difficulty, request.Title, request.Brief, request.SourceText,
                    request.FlawedTranslationText, request.WordCount, request.CreatedBy, request.Visibility,
                    request.MeaningCheckpoints, request.SeededErrors),
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        public record GenerateQuestionRequest(Guid ExamTypeId, TaskType TaskType, Difficulty Difficulty, Guid? CreatedBy);

        [HttpPost("generate")]
        public async Task<ActionResult<GenerateQuestionResult>> Generate(GenerateQuestionRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new GenerateQuestionCommand(request.ExamTypeId, request.TaskType, request.Difficulty, request.CreatedBy),
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GetQuestionByIdResult>> GetById(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetQuestionByIdQuery(id), cancellationToken));

        [HttpGet]
        public async Task<ActionResult<List<ListQuestionsResultItem>>> List(
            TaskType? taskType, Difficulty? difficulty, bool? inBank, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListQuestionsQuery(taskType, difficulty, inBank), cancellationToken));
    }
}
