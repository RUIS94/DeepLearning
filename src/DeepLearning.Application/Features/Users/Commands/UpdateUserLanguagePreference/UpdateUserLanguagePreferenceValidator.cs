using FluentValidation;

namespace DeepLearning.Application.Features.Users.Commands.UpdateUserLanguagePreference
{
    public class UpdateUserLanguagePreferenceValidator : AbstractValidator<UpdateUserLanguagePreferenceCommand>
    {
        private static readonly string[] Supported = { "en", "zh" };

        public UpdateUserLanguagePreferenceValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.LanguagePreference)
                .Must(x => Supported.Contains(x))
                .WithMessage("Language preference must be one of: en, zh.");
        }
    }
}
