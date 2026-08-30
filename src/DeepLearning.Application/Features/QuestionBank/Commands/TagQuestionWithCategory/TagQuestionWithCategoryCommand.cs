using MediatR;

namespace DeepLearning.Application.Features.QuestionBank.Commands.TagQuestionWithCategory
{
    /// <summary>
    /// Design doc §2.1 node C1/D1 "是否归入题库" -> CAT "选择领域/场景标签后存入题库": tagging a
    /// question with a category IS the "归入题库" action, so this also flips Question.InBank to
    /// true rather than requiring a separate call.
    /// </summary>
    public record TagQuestionWithCategoryCommand(Guid QuestionId, Guid CategoryId) : IRequest<TagQuestionWithCategoryResult>;
}
