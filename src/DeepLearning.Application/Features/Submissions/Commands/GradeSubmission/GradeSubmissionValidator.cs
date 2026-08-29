using FluentValidation;

namespace DeepLearning.Application.Features.Submissions.Commands.GradeSubmission
{
    public class GradeSubmissionValidator : AbstractValidator<GradeSubmissionCommand>
    {
        public GradeSubmissionValidator()
        {
            RuleFor(x => x.SubmissionId).NotEmpty();
            RuleFor(x => x.ExamTypeId).NotEmpty();
        }
    }
}
