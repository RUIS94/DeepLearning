using FluentValidation;

namespace DeepLearning.Application.Features.ReviewLibrary.Commands.MarkPatternReviewed
{
    public class MarkPatternReviewedValidator : AbstractValidator<MarkPatternReviewedCommand>
    {
        public MarkPatternReviewedValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.PatternId).NotEmpty();
            RuleFor(x => x.MasteryLevel).IsInEnum();
        }
    }
}
