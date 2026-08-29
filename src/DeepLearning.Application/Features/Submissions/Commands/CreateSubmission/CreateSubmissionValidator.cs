using System.Text.Json;
using DeepLearning.Domain.Enums;
using FluentValidation;

namespace DeepLearning.Application.Features.Submissions.Commands.CreateSubmission
{
    public class CreateSubmissionValidator : AbstractValidator<CreateSubmissionCommand>
    {
        public CreateSubmissionValidator()
        {
            RuleFor(x => x.QuestionId).NotEmpty();
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.TaskType).IsInEnum();

            RuleFor(x => x.Content)
                .NotEmpty()
                .Must(BeValidJson)
                .WithMessage("Content must be valid JSON.");

            // TaskA content is the plain translation as a JSON-encoded string; TaskB content is
            // a JSON array of annotation objects (position/errorCategory/correctedText — the
            // positions are offsets into the question's FlawedTranslationText, same convention
            // as TaskBSeededError). Same "don't trust a caller-shaped blob without checking it
            // matches TaskType" reasoning as ImportUserQuestionValidator.HaveConsistentTaskShape.
            RuleFor(x => x)
                .Must(HaveConsistentTaskShape)
                .WithMessage("TaskA Content must be a JSON string; TaskB Content must be a non-empty JSON array of {positionStart, positionEnd, errorCategory, correctedText} annotation objects.")
                .WithName(nameof(CreateSubmissionCommand.Content))
                .When(x => BeValidJson(x.Content));
        }

        private static bool HaveConsistentTaskShape(CreateSubmissionCommand command)
        {
            using var document = JsonDocument.Parse(command.Content);
            var root = document.RootElement;

            return command.TaskType switch
            {
                TaskType.A => root.ValueKind == JsonValueKind.String,
                TaskType.B => root.ValueKind == JsonValueKind.Array
                    && root.GetArrayLength() > 0
                    && root.EnumerateArray().All(IsValidAnnotation),
                _ => false,
            };
        }

        private static bool IsValidAnnotation(JsonElement element)
            => element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty("positionStart", out var start) && start.ValueKind == JsonValueKind.Number
                && element.TryGetProperty("positionEnd", out var end) && end.ValueKind == JsonValueKind.Number
                && element.TryGetProperty("errorCategory", out var category) && category.ValueKind == JsonValueKind.String && category.GetString() is { Length: > 0 }
                && element.TryGetProperty("correctedText", out var corrected) && corrected.ValueKind == JsonValueKind.String;

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
