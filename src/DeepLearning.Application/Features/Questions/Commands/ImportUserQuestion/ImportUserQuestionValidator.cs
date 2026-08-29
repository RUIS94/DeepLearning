using System.Text.Json;
using FluentValidation;

namespace DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion
{
    public class ImportUserQuestionValidator : AbstractValidator<ImportUserQuestionCommand>
    {
        public ImportUserQuestionValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
            RuleFor(x => x.SourceText).NotEmpty();
            RuleFor(x => x.TaskType).IsInEnum();
            RuleFor(x => x.Difficulty).IsInEnum();
            RuleFor(x => x.Visibility).IsInEnum();

            RuleFor(x => x.Brief)
                .Must(BeValidJsonOrNull)
                .WithMessage("Brief must be valid JSON.");

            RuleForEach(x => x.MeaningCheckpoints).ChildRules(checkpoint =>
            {
                checkpoint.RuleFor(x => x.CheckpointText).NotEmpty();
                checkpoint.RuleFor(x => x.Importance).IsInEnum();
            });

            // TaskA has no flawed translation / seeded errors; TaskB requires both,
            // and every seeded error's [start, end) range must fall inside the flawed
            // translation text with no two ranges overlapping — this is the "TaskB
            // error position validation logic" the design doc calls out as the
            // canonical unit-test target for this command.
            RuleFor(x => x)
                .Must(HaveConsistentTaskShape)
                .WithMessage("TaskA questions must not carry FlawedTranslationText/SeededErrors; TaskB questions must carry both.")
                .WithName(nameof(ImportUserQuestionCommand.TaskType));

            When(x => x.TaskType == Domain.Enums.TaskType.B, () =>
            {
                RuleForEach(x => x.SeededErrors).ChildRules(error =>
                {
                    error.RuleFor(x => x.PositionStart).GreaterThanOrEqualTo(0);
                    error.RuleFor(x => x.PositionEnd).GreaterThan(x => x.PositionStart);
                    error.RuleFor(x => x.CorrectReferenceText).NotEmpty();
                });

                RuleFor(x => x)
                    .Must(AllSeededErrorsFitWithinFlawedText)
                    .WithMessage("Every seeded error's position range must fall within FlawedTranslationText.")
                    .WithName(nameof(ImportUserQuestionCommand.SeededErrors));

                RuleFor(x => x.SeededErrors)
                    .Must(NotOverlap)
                    .WithMessage("Seeded error position ranges must not overlap.");
            });
        }

        private static bool HaveConsistentTaskShape(ImportUserQuestionCommand command) => command.TaskType switch
        {
            Domain.Enums.TaskType.A => string.IsNullOrEmpty(command.FlawedTranslationText) && command.SeededErrors.Count == 0,
            Domain.Enums.TaskType.B => !string.IsNullOrEmpty(command.FlawedTranslationText) && command.SeededErrors.Count > 0,
            _ => false,
        };

        private static bool AllSeededErrorsFitWithinFlawedText(ImportUserQuestionCommand command)
        {
            var length = command.FlawedTranslationText?.Length ?? 0;
            return command.SeededErrors.All(e => e.PositionStart >= 0 && e.PositionEnd <= length);
        }

        private static bool NotOverlap(List<SeededErrorInput> errors)
        {
            var sorted = errors.OrderBy(e => e.PositionStart).ToList();
            for (var i = 1; i < sorted.Count; i++)
            {
                if (sorted[i].PositionStart < sorted[i - 1].PositionEnd)
                {
                    return false;
                }
            }

            return true;
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
