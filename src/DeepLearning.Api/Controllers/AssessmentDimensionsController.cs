using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateAssessmentDimension;
using DeepLearning.Application.Features.ExamConfig.Queries.GetAssessmentDimensionsByExamType;
using DeepLearning.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.AssessmentDimensions.Base)]
    public class AssessmentDimensionsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AssessmentDimensionsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        public record CreateAssessmentDimensionRequest(
            string DimensionKey,
            string DimensionName,
            ScaleType ScaleType,
            string? PassThreshold,
            TaskType? ApplicableTaskType,
            string LevelDescriptions,
            string RubricVersion,
            DateTimeOffset EffectiveFrom,
            DateTimeOffset? EffectiveTo,
            string? SourceReference);

        [HttpPost]
        public async Task<ActionResult<CreateAssessmentDimensionResult>> Create(
            Guid examTypeId, CreateAssessmentDimensionRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CreateAssessmentDimensionCommand(
                    examTypeId, request.DimensionKey, request.DimensionName, request.ScaleType, request.PassThreshold,
                    request.ApplicableTaskType, request.LevelDescriptions, request.RubricVersion,
                    request.EffectiveFrom, request.EffectiveTo, request.SourceReference),
                cancellationToken);

            return CreatedAtAction(nameof(List), new { examTypeId }, result);
        }

        [HttpGet]
        public async Task<ActionResult<List<AssessmentDimensionResultItem>>> List(
            Guid examTypeId, TaskType? applicableTaskType, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetAssessmentDimensionsByExamTypeQuery(examTypeId, applicableTaskType), cancellationToken));
    }
}
