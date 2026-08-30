using FluentValidation;

namespace DeepLearning.Application.Features.ReviewLibrary.Commands.MarkVocabReviewed
{
    public class MarkVocabReviewedValidator : AbstractValidator<MarkVocabReviewedCommand>
    {
        public MarkVocabReviewedValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.VocabId).NotEmpty();
            RuleFor(x => x.MasteryLevel).IsInEnum();
        }
    }
}
