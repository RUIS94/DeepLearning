using System.Text.Json;
using FluentValidation;

namespace DeepLearning.Application.Features.ExamConfig.Commands.CreateAssessmentDimension
{
    public class CreateAssessmentDimensionValidator : AbstractValidator<CreateAssessmentDimensionCommand>
    {
        public CreateAssessmentDimensionValidator()
        {
            RuleFor(x => x.ExamTypeId).NotEmpty();
            RuleFor(x => x.DimensionKey).NotEmpty().MaximumLength(50);
            RuleFor(x => x.DimensionName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.ScaleType).IsInEnum();
            RuleFor(x => x.PassThreshold).MaximumLength(20);
            RuleFor(x => x.RubricVersion).NotEmpty().MaximumLength(20);

            RuleFor(x => x.LevelDescriptions)
                .NotEmpty()
                .Must(BeValidJson)
                .WithMessage("LevelDescriptions must be valid JSON.");

            RuleFor(x => x)
                .Must(x => x.EffectiveTo is null || x.EffectiveTo > x.EffectiveFrom)
                .WithMessage("EffectiveTo must be later than EffectiveFrom.")
                .WithName(nameof(CreateAssessmentDimensionCommand.EffectiveTo));
        }

        private static bool BeValidJson(string value)
        {
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
