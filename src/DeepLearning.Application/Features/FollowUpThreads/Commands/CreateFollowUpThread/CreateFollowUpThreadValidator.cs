using DeepLearning.Domain.Enums;
using FluentValidation;

namespace DeepLearning.Application.Features.FollowUpThreads.Commands.CreateFollowUpThread
{
    public class CreateFollowUpThreadValidator : AbstractValidator<CreateFollowUpThreadCommand>
    {
        public CreateFollowUpThreadValidator()
        {
            RuleFor(x => x.SubmissionId).NotEmpty();
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.ExamTypeId).NotEmpty();
            RuleFor(x => x.QuestionText).NotEmpty();
            RuleFor(x => x.ContextRef).MaximumLength(100);

            // DimensionId is the anchor for a score challenge and meaningless otherwise.
            RuleFor(x => x.DimensionId)
                .NotEmpty()
                .When(x => x.Kind == FollowUpThreadKind.score_challenge)
                .WithMessage("A score_challenge thread requires DimensionId (the dimension whose Band is challenged).");
            RuleFor(x => x.DimensionId)
                .Empty()
                .When(x => x.Kind != FollowUpThreadKind.score_challenge)
                .WithMessage("DimensionId is only valid when Kind is score_challenge.");
        }
    }
}
