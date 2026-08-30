using FluentValidation;

namespace DeepLearning.Application.Features.StandardOverrides.Commands.ActivateStandardOverride
{
    public class ActivateStandardOverrideValidator : AbstractValidator<ActivateStandardOverrideCommand>
    {
        public ActivateStandardOverrideValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
        }
    }
}
