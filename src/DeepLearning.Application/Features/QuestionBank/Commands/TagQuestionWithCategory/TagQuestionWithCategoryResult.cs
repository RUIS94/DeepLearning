namespace DeepLearning.Application.Features.QuestionBank.Commands.TagQuestionWithCategory
{
    public record TagQuestionWithCategoryResult(Guid QuestionId, Guid CategoryId, bool InBank);
}
