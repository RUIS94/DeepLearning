using FluentValidation;

namespace DeepLearning.Application.Features.QuestionBank.Commands.CreateQuestionBankCategory
{
    public class CreateQuestionBankCategoryValidator : AbstractValidator<CreateQuestionBankCategoryCommand>
    {
        public CreateQuestionBankCategoryValidator()
        {
            RuleFor(x => x.CategoryType).IsInEnum();
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        }
    }
}
