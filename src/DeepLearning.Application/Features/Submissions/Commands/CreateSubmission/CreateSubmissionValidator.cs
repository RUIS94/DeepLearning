using System.Text.Json;
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
