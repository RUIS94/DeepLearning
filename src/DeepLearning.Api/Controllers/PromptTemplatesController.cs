using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreatePromptTemplate;
using DeepLearning.Application.Features.ExamConfig.Commands.DeletePromptTemplate;
using DeepLearning.Application.Features.ExamConfig.Commands.UpdatePromptTemplate;
using DeepLearning.Application.Features.ExamConfig.Queries.GetPromptTemplatesByExamType;
using DeepLearning.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.PromptTemplates.Base)]
    public class PromptTemplatesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public PromptTemplatesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        public record CreatePromptTemplateRequest(
            Guid? ExamTypeId,
            SubjectCategory? SubjectCategory,
            AiOperationType TemplateType,
            TemplateLayer Layer,
            string TemplateContent,
            int Version);

        [HttpPost]
        public async Task<ActionResult<CreatePromptTemplateResult>> Create(CreatePromptTemplateRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CreatePromptTemplateCommand(
                    request.ExamTypeId, request.SubjectCategory, request.TemplateType, request.Layer,
                    request.TemplateContent, request.Version),
                cancellationToken);

            return CreatedAtAction(nameof(List), null, result);
        }

        [HttpGet]
        public async Task<ActionResult<List<PromptTemplateResultItem>>> List(
            Guid? examTypeId, SubjectCategory? subjectCategory, AiOperationType? templateType, bool? isActive,
            CancellationToken cancellationToken)
            => Ok(await _mediator.Send(
                new GetPromptTemplatesByExamTypeQuery(examTypeId, subjectCategory, templateType, isActive),
                cancellationToken));

        public record UpdatePromptTemplateRequest(string TemplateContent, int Version, bool IsActive);

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<UpdatePromptTemplateResult>> Update(
            Guid id, UpdatePromptTemplateRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(
                new UpdatePromptTemplateCommand(id, request.TemplateContent, request.Version, request.IsActive),
                cancellationToken));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            await _mediator.Send(new DeletePromptTemplateCommand(id), cancellationToken);
            return NoContent();
        }
    }
}
