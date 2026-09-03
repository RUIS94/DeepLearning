using FluentValidation;

namespace DeepLearning.Application.Features.WeakPointCatalogAdmin.Commands.UpdateWeakPointCatalogEntry
{
    public class UpdateWeakPointCatalogEntryValidator : AbstractValidator<UpdateWeakPointCatalogEntryCommand>
    {
        public UpdateWeakPointCatalogEntryValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.Name).MaximumLength(100).When(x => x.Name is not null);
            RuleFor(x => x.DefaultDimensionKey).MaximumLength(50).When(x => x.DefaultDimensionKey is not null);
            RuleFor(x => x.DefaultErrorCategory).MaximumLength(50).When(x => x.DefaultErrorCategory is not null);
            RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        }
    }
}
