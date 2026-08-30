using FluentValidation;

namespace DeepLearning.Application.Features.Questions.Commands.GenerateQuestion
{
    public class GenerateQuestionValidator : AbstractValidator<GenerateQuestionCommand>
    {
        public const int MaxSeedQuestionIds = 5;

        public GenerateQuestionValidator()
        {
            RuleFor(x => x.ExamTypeId).NotEmpty();
            RuleFor(x => x.TaskType).IsInEnum();
            When(x => x.Difficulty.HasValue, () => RuleFor(x => x.Difficulty!.Value).IsInEnum());

            When(x => x.SeedQuestionIds is not null, () =>
            {
                RuleForEach(x => x.SeedQuestionIds).NotEmpty();
                RuleFor(x => x.SeedQuestionIds!)
                    .Must(ids => ids.Count <= MaxSeedQuestionIds)
                    .WithMessage($"No more than {MaxSeedQuestionIds} seed questions may be specified per generation call.");
                RuleFor(x => x.SeedQuestionIds!)
                    .Must(ids => ids.Distinct().Count() == ids.Count)
                    .WithMessage("SeedQuestionIds must not contain duplicates.");
            });
        }
    }
}
