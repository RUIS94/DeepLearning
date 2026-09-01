using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.QuestionBank.Commands.CreateQuestionBankCategory;
using DeepLearning.Application.Features.QuestionBank.Commands.DeleteQuestionBankCategory;
using DeepLearning.Application.Features.QuestionBank.Commands.TagQuestionWithCategory;
using DeepLearning.Application.Features.QuestionBank.Commands.UpdateQuestionBankCategory;
using DeepLearning.Application.Features.QuestionBank.Queries.GetQuestionBankCategoryById;
using DeepLearning.Application.Features.QuestionBank.Queries.ListQuestionBankCategories;
using DeepLearning.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DeepLearning.Api.Controllers
{
    [ApiController]
    [Route(ApiRoutes.QuestionBankCategories.Base)]
    public class QuestionBankCategoriesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public QuestionBankCategoriesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        public record CreateQuestionBankCategoryRequest(CategoryType CategoryType, string Name, Guid? ParentId, string? Description);

        [HttpPost]
        public async Task<ActionResult<CreateQuestionBankCategoryResult>> Create(CreateQuestionBankCategoryRequest request, CancellationToken cancellationToken)
        {
            var result = await _mediator.Send(
                new CreateQuestionBankCategoryCommand(request.CategoryType, request.Name, request.ParentId, request.Description),
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<GetQuestionBankCategoryByIdResult>> GetById(Guid id, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new GetQuestionBankCategoryByIdQuery(id), cancellationToken));

        public record UpdateQuestionBankCategoryRequest(string Name, Guid? ParentId, string? Description);

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<UpdateQuestionBankCategoryResult>> Update(
            Guid id, UpdateQuestionBankCategoryRequest request, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(
                new UpdateQuestionBankCategoryCommand(id, request.Name, request.ParentId, request.Description),
                cancellationToken));

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            await _mediator.Send(new DeleteQuestionBankCategoryCommand(id), cancellationToken);
            return NoContent();
        }

        [HttpGet]
        public async Task<ActionResult<List<ListQuestionBankCategoriesResultItem>>> List(CategoryType? categoryType, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new ListQuestionBankCategoriesQuery(categoryType), cancellationToken));

        // Design doc §2.1 node C1/D1 "是否归入题库" -> CAT: tagging a question with a category
        // here is the "归入题库" action itself (see TagQuestionWithCategoryCommand's own doc).
        [HttpPost("{categoryId:guid}/questions/{questionId:guid}")]
        public async Task<ActionResult<TagQuestionWithCategoryResult>> TagQuestion(Guid categoryId, Guid questionId, CancellationToken cancellationToken)
            => Ok(await _mediator.Send(new TagQuestionWithCategoryCommand(questionId, categoryId), cancellationToken));
    }
}
