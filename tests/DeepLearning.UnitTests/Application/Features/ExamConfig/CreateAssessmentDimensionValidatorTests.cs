using DeepLearning.Application.Features.ExamConfig.Commands.CreateAssessmentDimension;
using DeepLearning.Domain.Enums;

namespace DeepLearning.UnitTests.Application.Features.ExamConfig
{
    public class CreateAssessmentDimensionValidatorTests
    {
        private readonly CreateAssessmentDimensionValidator _validator = new();

        private static CreateAssessmentDimensionCommand ValidCommand(DateTimeOffset from, DateTimeOffset? to) => new(
            ExamTypeId: Guid.NewGuid(),
            DimensionKey: "meaning_transfer",
            DimensionName: "Meaning transfer",
            ScaleType: ScaleType.band_1_5,
            PassThreshold: "Band 2 or above",
            ApplicableTaskType: TaskType.A,
            LevelDescriptions: "{\"1\":\"...\"}",
            RubricVersion: "2024-02",
            EffectiveFrom: from,
            EffectiveTo: to,
            SourceReference: null);

        [Fact]
        public void Passes_when_effective_to_is_null()
        {
            var result = _validator.Validate(ValidCommand(DateTimeOffset.UtcNow, null));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Passes_when_effective_to_is_after_effective_from()
        {
            var from = DateTimeOffset.UtcNow;
            var result = _validator.Validate(ValidCommand(from, from.AddDays(1)));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Fails_when_effective_to_is_before_effective_from()
        {
            var from = DateTimeOffset.UtcNow;
            var result = _validator.Validate(ValidCommand(from, from.AddDays(-1)));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateAssessmentDimensionCommand.EffectiveTo));
        }

        [Fact]
        public void Fails_when_effective_to_equals_effective_from()
        {
            var from = DateTimeOffset.UtcNow;
            var result = _validator.Validate(ValidCommand(from, from));

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Fails_when_level_descriptions_is_not_valid_json()
        {
            var command = ValidCommand(DateTimeOffset.UtcNow, null) with { LevelDescriptions = "not json" };

            var result = _validator.Validate(command);

            Assert.False(result.IsValid);
        }
    }
}
