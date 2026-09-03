using FluentValidation;

namespace DeepLearning.Application.Features.WeakPointCatalogAdmin.Commands.MergeWeakPointCatalog
{
    public class MergeWeakPointCatalogValidator : AbstractValidator<MergeWeakPointCatalogCommand>
    {
        public MergeWeakPointCatalogValidator()
        {
            RuleFor(x => x.FromId).NotEmpty();
            RuleFor(x => x.ToId).NotEmpty().NotEqual(x => x.FromId);
        }
    }
}
