using DeepLearning.Application.Features.Questions.Commands.GenerateQuestion;
using DeepLearning.Domain.Enums;

namespace DeepLearning.UnitTests.Application.Features.Questions
{
    public class GenerateQuestionValidatorTests
    {
        private readonly GenerateQuestionValidator _validator = new();

        private static GenerateQuestionCommand ValidCommand(List<Guid>? seedQuestionIds) => new(
            ExamTypeId: Guid.NewGuid(),
            TaskType: TaskType.A,
            Difficulty: Difficulty.medium,
            CategoryId: null,
            SeedQuestionIds: seedQuestionIds,
            CreatedBy: null);

        [Fact]
        public void Passes_when_seed_question_ids_is_null()
        {
            var result = _validator.Validate(ValidCommand(null));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Passes_when_seed_question_ids_is_within_the_cap_and_has_no_duplicates()
        {
            var ids = Enumerable.Range(0, GenerateQuestionValidator.MaxSeedQuestionIds).Select(_ => Guid.NewGuid()).ToList();

            var result = _validator.Validate(ValidCommand(ids));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Fails_when_seed_question_ids_exceeds_the_cap()
        {
            var ids = Enumerable.Range(0, GenerateQuestionValidator.MaxSeedQuestionIds + 1).Select(_ => Guid.NewGuid()).ToList();

            var result = _validator.Validate(ValidCommand(ids));

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Fails_when_seed_question_ids_contains_a_duplicate()
        {
            var duplicate = Guid.NewGuid();

            var result = _validator.Validate(ValidCommand([duplicate, duplicate]));

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Fails_when_seed_question_ids_contains_an_empty_guid()
        {
            var result = _validator.Validate(ValidCommand([Guid.Empty]));

            Assert.False(result.IsValid);
        }
    }
}
