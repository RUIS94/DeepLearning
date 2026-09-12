using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.ExamConfig.Commands.SetExamTypeActivation;
using DeepLearning.Application.Features.ExamConfig.Queries.GetExamTypeById;
using DeepLearning.Application.Features.ExamConfig.Queries.ListExamTypes;
using DeepLearning.Application.Features.ExamConfig.Queries.ListMyExamTypeActivations;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.ExamTypes.Base)]
    public class ExamTypesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUser;

        public ExamTypesController(IMediator mediator, ICurrentUserService currentUser)
        {
            _mediator = mediator;
            _currentUser = currentUser;
        }

        public record CreateExamTypeRequest(
            string Code,
            string Name,
            SubjectCategory SubjectCategory,
            string? SourceLanguage,
            string? TargetLanguage,
            string? GradeLevel,
            string? Description);

        [Authorize(Policy = "AdminOnly")]
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

        /// <summary>
        /// U3 (ref/管理员与用户权限隔离_策划书.md Phase 4) — every globally-active exam type,
        /// each annotated with whether the caller has personally activated it. Net-new capability;
        /// no frontend consumes it yet since the app currently only ever has one exam type.
        /// </summary>
        [HttpGet("mine")]
        public async Task<ActionResult<List<ExamTypeActivationResultItem>>> ListMine(CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListMyExamTypeActivationsQuery(_currentUser.RequiredUserId), cancellationToken));

        public record SetExamTypeActivationRequest(bool IsActive);

        [HttpPut("{id:guid}/activation")]
        public async Task<ActionResult<SetExamTypeActivationResult>> SetActivation(
            Guid id, SetExamTypeActivationRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(
                new SetExamTypeActivationCommand(_currentUser.RequiredUserId, id, request.IsActive), cancellationToken));
    }
}
