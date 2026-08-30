using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence.Repositories;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Integration
{
    [Collection(PostgresCollection.Name)]
    public class QuestionBankRepositoryTests
    {
        private readonly PostgresContainerFixture _fixture;

        public QuestionBankRepositoryTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        private static Question NewQuestion(TaskType taskType, Difficulty difficulty, bool isSeedReference) => new()
        {
            Id = Guid.NewGuid(),
            TaskType = taskType,
            Difficulty = difficulty,
            Title = $"seed_{Guid.NewGuid():N}",
            SourceText = "Some real-exam-shaped source text.",
            Origin = QuestionOrigin.user_uploaded,
            SourceType = isSeedReference ? SourceType.real_exam : SourceType.user_generated,
            IsSeedReference = isSeedReference,
            Visibility = Visibility.Private,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        [Fact]
        public async Task ListSeedReferenceCandidatesAsync_only_returns_active_seed_reference_questions_matching_task_type_and_difficulty()
        {
            await using var context = _fixture.CreateContext();
            var repository = new QuestionRepository(context);

            var matchingSeed = NewQuestion(TaskType.A, Difficulty.medium, isSeedReference: true);
            var wrongDifficulty = NewQuestion(TaskType.A, Difficulty.hard, isSeedReference: true);
            var wrongTaskType = NewQuestion(TaskType.B, Difficulty.medium, isSeedReference: true);
            var notASeed = NewQuestion(TaskType.A, Difficulty.medium, isSeedReference: false);

            await repository.AddAsync(matchingSeed);
            await repository.AddAsync(wrongDifficulty);
            await repository.AddAsync(wrongTaskType);
            await repository.AddAsync(notASeed);
            await context.SaveChangesAsync();

            // take is generous and assertions check membership rather than exact count/order —
            // PostgresCollection shares one DB across every test in this collection, so an
            // unfiltered (no categoryId) task-type+difficulty query can also see rows other
            // tests seeded; membership is still deterministic proof of the filter logic.
            var candidates = await repository.ListSeedReferenceCandidatesAsync(TaskType.A, Difficulty.medium, categoryId: null, take: 100);

            Assert.Contains(candidates, c => c.Id == matchingSeed.Id);
            Assert.DoesNotContain(candidates, c => c.Id == wrongDifficulty.Id);
            Assert.DoesNotContain(candidates, c => c.Id == wrongTaskType.Id);
            Assert.DoesNotContain(candidates, c => c.Id == notASeed.Id);
        }

        [Fact]
        public async Task ListSeedReferenceCandidatesAsync_narrows_to_a_category_when_one_is_supplied()
        {
            await using var context = _fixture.CreateContext();
            var repository = new QuestionRepository(context);

            var category = new QuestionBankCategory
            {
                Id = Guid.NewGuid(),
                CategoryType = CategoryType.domain,
                Name = $"legal_{Guid.NewGuid():N}",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await context.QuestionBankCategories.AddAsync(category);

            var taggedSeed = NewQuestion(TaskType.A, Difficulty.medium, isSeedReference: true);
            var untaggedSeed = NewQuestion(TaskType.A, Difficulty.medium, isSeedReference: true);
            await repository.AddAsync(taggedSeed);
            await repository.AddAsync(untaggedSeed);
            await context.SaveChangesAsync();

            await repository.AddCategoryMapAsync(new QuestionCategoryMap { Id = Guid.NewGuid(), QuestionId = taggedSeed.Id, CategoryId = category.Id });
            await context.SaveChangesAsync();

            var candidates = await repository.ListSeedReferenceCandidatesAsync(TaskType.A, Difficulty.medium, category.Id, take: 10);

            Assert.Single(candidates);
            Assert.Equal(taggedSeed.Id, candidates[0].Id);
        }

        [Fact]
        public async Task Tagging_a_question_with_a_category_is_reflected_in_ListCategoryIdsAsync_and_HasCategoryMapAsync()
        {
            await using var context = _fixture.CreateContext();
            var repository = new QuestionRepository(context);

            var category = new QuestionBankCategory
            {
                Id = Guid.NewGuid(),
                CategoryType = CategoryType.scenario,
                Name = $"immigration_{Guid.NewGuid():N}",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await context.QuestionBankCategories.AddAsync(category);

            var question = NewQuestion(TaskType.A, Difficulty.easy, isSeedReference: false);
            await repository.AddAsync(question);
            await context.SaveChangesAsync();

            Assert.False(await repository.HasCategoryMapAsync(question.Id, category.Id));

            await repository.AddCategoryMapAsync(new QuestionCategoryMap { Id = Guid.NewGuid(), QuestionId = question.Id, CategoryId = category.Id });
            await context.SaveChangesAsync();

            Assert.True(await repository.HasCategoryMapAsync(question.Id, category.Id));
            var categoryIds = await repository.ListCategoryIdsAsync(question.Id);
            Assert.Contains(category.Id, categoryIds);
        }
    }
}
