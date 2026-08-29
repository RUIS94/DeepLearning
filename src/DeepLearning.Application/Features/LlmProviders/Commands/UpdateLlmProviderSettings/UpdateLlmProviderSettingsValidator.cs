using System.Text.Json;
using FluentValidation;

namespace DeepLearning.Application.Features.LlmProviders.Commands.UpdateLlmProviderSettings
{
    public class UpdateLlmProviderSettingsValidator : AbstractValidator<UpdateLlmProviderSettingsCommand>
    {
        public UpdateLlmProviderSettingsValidator()
        {
            RuleFor(x => x.ProviderKey).NotEmpty();
            RuleFor(x => x.Model).MaximumLength(100);
            RuleFor(x => x.Effort).MaximumLength(20);

            RuleFor(x => x.ExtraSettingsJson)
                .Must(BeValidJsonOrNull)
                .WithMessage("ExtraSettingsJson must be valid JSON.");
        }

        private static bool BeValidJsonOrNull(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            try
            {
                using var _ = JsonDocument.Parse(value);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
