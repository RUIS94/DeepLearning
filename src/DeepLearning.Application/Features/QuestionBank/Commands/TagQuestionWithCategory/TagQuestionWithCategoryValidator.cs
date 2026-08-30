using FluentValidation;

namespace DeepLearning.Application.Features.QuestionBank.Commands.TagQuestionWithCategory
{
    public class TagQuestionWithCategoryValidator : AbstractValidator<TagQuestionWithCategoryCommand>
    {
        public TagQuestionWithCategoryValidator()
        {
            RuleFor(x => x.QuestionId).NotEmpty();
            RuleFor(x => x.CategoryId).NotEmpty();
        }
    }
}
