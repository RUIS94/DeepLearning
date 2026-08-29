using FluentValidation;

namespace DeepLearning.Application.Features.ExamConfig.Commands.CreateErrorTaxonomy
{
    public class CreateErrorTaxonomyValidator : AbstractValidator<CreateErrorTaxonomyCommand>
    {
        public CreateErrorTaxonomyValidator()
        {
            RuleFor(x => x.ExamTypeId).NotEmpty();
            RuleFor(x => x.CategoryKey).NotEmpty().MaximumLength(50);
            RuleFor(x => x.CategoryName).NotEmpty().MaximumLength(100);
        }
    }
}
