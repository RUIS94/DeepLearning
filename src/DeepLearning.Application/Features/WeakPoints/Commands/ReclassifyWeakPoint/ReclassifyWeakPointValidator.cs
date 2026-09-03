using FluentValidation;

namespace DeepLearning.Application.Features.WeakPoints.Commands.ReclassifyWeakPoint
{
    public class ReclassifyWeakPointValidator : AbstractValidator<ReclassifyWeakPointCommand>
    {
        public ReclassifyWeakPointValidator()
        {
            RuleFor(x => x.WeakPointId).NotEmpty();
            RuleFor(x => x.CatalogId).NotEmpty();
        }
    }
}
