using FluentValidation;

namespace DeepLearning.Application.Features.QuestionBank.Commands.UpdateQuestionBankCategory
{
    public class UpdateQuestionBankCategoryValidator : AbstractValidator<UpdateQuestionBankCategoryCommand>
    {
        public UpdateQuestionBankCategoryValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.ParentId).NotEqual(x => x.Id).WithMessage("A category cannot be its own parent.");
        }
    }
}
