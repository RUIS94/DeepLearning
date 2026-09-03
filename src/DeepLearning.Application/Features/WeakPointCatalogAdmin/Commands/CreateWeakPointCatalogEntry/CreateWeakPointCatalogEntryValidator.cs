using FluentValidation;

namespace DeepLearning.Application.Features.WeakPointCatalogAdmin.Commands.CreateWeakPointCatalogEntry
{
    public class CreateWeakPointCatalogEntryValidator : AbstractValidator<CreateWeakPointCatalogEntryCommand>
    {
        public CreateWeakPointCatalogEntryValidator()
        {
            RuleFor(x => x.ExamTypeId).NotEmpty();
            RuleFor(x => x.Code).NotEmpty().MaximumLength(60)
                .Matches("^[a-z0-9_]+$").WithMessage("Code must be lower_snake_case (a-z, 0-9, underscore).");
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Description).NotEmpty();
            RuleFor(x => x.DefaultDimensionKey).MaximumLength(50);
            RuleFor(x => x.DefaultErrorCategory).MaximumLength(50);
        }
    }
}
