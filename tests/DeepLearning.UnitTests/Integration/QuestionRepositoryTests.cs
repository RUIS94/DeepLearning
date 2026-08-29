using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence.Repositories;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Integration
{
    [Collection(PostgresCollection.Name)]
    public class QuestionRepositoryTests
    {
        private readonly PostgresContainerFixture _fixture;

        public QuestionRepositoryTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task A_task_b_question_round_trips_with_its_checkpoints_and_seeded_errors_joined_across_three_tables()
        {
            const string flawedText = "This sentence has an error in it.";

            await using var context = _fixture.CreateContext();

            var examType = new ExamType
            {
                Id = Guid.NewGuid(),
                Code = $"test_{Guid.NewGuid():N}",
                Name = "Integration Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var errorTaxonomy = new ErrorTaxonomy
            {
                Id = Guid.NewGuid(),
                ExamTypeId = examType.Id,
                CategoryKey = "distortion",
                CategoryName = "Distortion",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await context.ExamTypes.AddAsync(examType);
            await context.ErrorTaxonomies.AddAsync(errorTaxonomy);
            await context.SaveChangesAsync();

            var repository = new QuestionRepository(context);
            var question = new Question
            {
                Id = Guid.NewGuid(),
                TaskType = TaskType.B,
                Difficulty = Difficulty.medium,
                Title = "Integration Test Question",
                SourceText = "Original source text.",
                FlawedTranslationText = flawedText,
                Origin = QuestionOrigin.user_uploaded,
                SourceType = SourceType.user_generated,
                Visibility = Visibility.Private,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await repository.AddAsync(question);
            await repository.AddMeaningCheckpointsAsync([
                new MeaningCheckpoint
                {
                    Id = Guid.NewGuid(),
                    QuestionId = question.Id,
                    CheckpointText = "Must convey the core fact.",
                    Importance = CheckpointImportance.core,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
            ]);
            await repository.AddSeededErrorsAsync([
                new TaskBSeededError
                {
                    Id = Guid.NewGuid(),
                    QuestionId = question.Id,
                    PositionStart = 9,
                    PositionEnd = 17,
                    ErrorTaxonomyId = errorTaxonomy.Id,
                    CorrectReferenceText = "had",
                    CreatedAt = DateTimeOffset.UtcNow,
                },
            ]);
            await context.SaveChangesAsync();

            await using var readContext = _fixture.CreateContext();
            var readRepository = new QuestionRepository(readContext);

            var fetchedQuestion = await readRepository.GetByIdAsync(question.Id);
            var checkpoints = await readRepository.GetMeaningCheckpointsAsync(question.Id);
            var seededErrors = await readRepository.GetSeededErrorsAsync(question.Id);

            Assert.NotNull(fetchedQuestion);
            Assert.Equal(TaskType.B, fetchedQuestion!.TaskType);
            Assert.Single(checkpoints);
            Assert.Single(seededErrors);
            Assert.Equal("distortion", seededErrors[0].ErrorTaxonomy?.CategoryKey);
        }
    }
}
