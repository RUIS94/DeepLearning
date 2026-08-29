using DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion;
using DeepLearning.Domain.Enums;

namespace DeepLearning.UnitTests.Application.Features.Questions
{
    public class ImportUserQuestionValidatorTests
    {
        private readonly ImportUserQuestionValidator _validator = new();

        private static ImportUserQuestionCommand TaskACommand() => new(
            TaskType: TaskType.A,
            Difficulty: Difficulty.medium,
            Title: "Sample title",
            Brief: null,
            SourceText: "Some source text long enough to translate.",
            FlawedTranslationText: null,
            WordCount: 250,
            CreatedBy: null,
            Visibility: Visibility.Private,
            MeaningCheckpoints: [],
            SeededErrors: []);

        private static ImportUserQuestionCommand TaskBCommand(string flawedText, List<SeededErrorInput> seededErrors) => new(
            TaskType: TaskType.B,
            Difficulty: Difficulty.medium,
            Title: "Sample title",
            Brief: null,
            SourceText: "Some source text long enough to translate.",
            FlawedTranslationText: flawedText,
            WordCount: 250,
            CreatedBy: null,
            Visibility: Visibility.Private,
            MeaningCheckpoints: [],
            SeededErrors: seededErrors);

        [Fact]
        public void Passes_for_a_well_formed_task_a_question()
        {
            Assert.True(_validator.Validate(TaskACommand()).IsValid);
        }

        [Fact]
        public void Fails_when_task_a_carries_seeded_errors()
        {
            var command = TaskACommand() with
            {
                SeededErrors = [new SeededErrorInput(0, 5, Guid.NewGuid(), "fix", null)],
            };

            Assert.False(_validator.Validate(command).IsValid);
        }

        [Fact]
        public void Passes_for_a_well_formed_task_b_question()
        {
            const string flawed = "This sentence has an error in it.";
            var command = TaskBCommand(flawed, [new SeededErrorInput(9, 17, Guid.NewGuid(), "had", null)]);

            Assert.True(_validator.Validate(command).IsValid);
        }

        [Fact]
        public void Fails_when_task_b_has_no_flawed_translation_text()
        {
            var command = TaskBCommand(string.Empty, [new SeededErrorInput(0, 5, Guid.NewGuid(), "fix", null)]);

            Assert.False(_validator.Validate(command).IsValid);
        }

        [Fact]
        public void Fails_when_task_b_has_no_seeded_errors()
        {
            var command = TaskBCommand("Some flawed text.", []);

            Assert.False(_validator.Validate(command).IsValid);
        }

        [Fact]
        public void Fails_when_a_seeded_error_position_exceeds_the_flawed_text_length()
        {
            const string flawed = "Short text.";
            var command = TaskBCommand(flawed, [new SeededErrorInput(5, flawed.Length + 10, Guid.NewGuid(), "fix", null)]);

            Assert.False(_validator.Validate(command).IsValid);
        }

        [Fact]
        public void Fails_when_a_seeded_error_end_is_not_after_its_start()
        {
            const string flawed = "Short text.";
            var command = TaskBCommand(flawed, [new SeededErrorInput(5, 5, Guid.NewGuid(), "fix", null)]);

            Assert.False(_validator.Validate(command).IsValid);
        }

        [Fact]
        public void Fails_when_two_seeded_errors_overlap()
        {
            const string flawed = "This sentence has an error in it, really.";
            var command = TaskBCommand(flawed, [
                new SeededErrorInput(5, 15, Guid.NewGuid(), "fix1", null),
                new SeededErrorInput(10, 20, Guid.NewGuid(), "fix2", null),
            ]);

            Assert.False(_validator.Validate(command).IsValid);
        }

        [Fact]
        public void Passes_when_two_seeded_errors_are_adjacent_but_not_overlapping()
        {
            const string flawed = "This sentence has an error in it, really.";
            var command = TaskBCommand(flawed, [
                new SeededErrorInput(5, 10, Guid.NewGuid(), "fix1", null),
                new SeededErrorInput(10, 15, Guid.NewGuid(), "fix2", null),
            ]);

            Assert.True(_validator.Validate(command).IsValid);
        }
    }
}
