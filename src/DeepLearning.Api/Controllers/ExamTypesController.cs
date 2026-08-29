using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.ExamConfig.Queries.GetExamTypeById;
using DeepLearning.Application.Features.ExamConfig.Queries.ListExamTypes;
using DeepLearning.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.ExamTypes.Base)]
    public class ExamTypesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ExamTypesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        public record CreateExamTypeRequest(
            string Code,
            string Name,
            SubjectCategory SubjectCategory,
            string? SourceLanguage,
            string? TargetLanguage,
            string? GradeLevel,
            string? Description);

        [HttpPost]
        public async Task<ActionResult<CreateExamTypeResult>> Create(CreateExamTypeRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CreateExamTypeCommand(
                    request.Code, request.Name, request.SubjectCategory,
                    request.SourceLanguage, request.TargetLanguage, request.GradeLevel, request.Description),
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GetExamTypeByIdResult>> GetById(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetExamTypeByIdQuery(id), cancellationToken));

        [HttpGet]
        public async Task<ActionResult<List<ListExamTypesResultItem>>> List(bool? isActive, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListExamTypesQuery(isActive), cancellationToken));
    }
}
