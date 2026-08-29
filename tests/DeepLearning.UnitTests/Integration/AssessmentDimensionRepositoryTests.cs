using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence.Repositories;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Integration
{
    [Collection(PostgresCollection.Name)]
    public class AssessmentDimensionRepositoryTests
    {
        private readonly PostgresContainerFixture _fixture;

        public AssessmentDimensionRepositoryTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<ExamType> CreateExamTypeAsync()
        {
            await using var context = _fixture.CreateContext();
            var examType = new ExamType
            {
                Id = Guid.NewGuid(),
                Code = $"test_{Guid.NewGuid():N}",
                Name = "Integration Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await context.ExamTypes.AddAsync(examType);
            await context.SaveChangesAsync();
            return examType;
        }

        [Fact]
        public async Task Add_then_list_by_exam_type_round_trips_through_a_real_database()
        {
            var examType = await CreateExamTypeAsync();

            await using var context = _fixture.CreateContext();
            var repository = new AssessmentDimensionRepository(context);
            await repository.AddAsync(new AssessmentDimension
            {
                Id = Guid.NewGuid(),
                ExamTypeId = examType.Id,
                DimensionKey = "meaning_transfer",
                DimensionName = "Meaning transfer",
                ScaleType = ScaleType.band_1_5,
                ApplicableTaskType = TaskType.A,
                LevelDescriptions = "{\"1\":\"...\"}",
                RubricVersion = "2024-02",
                EffectiveFrom = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();

            await using var readContext = _fixture.CreateContext();
            var readRepository = new AssessmentDimensionRepository(readContext);
            var results = await readRepository.ListByExamTypeAsync(examType.Id, TaskType.A);

            Assert.Single(results);
            Assert.Equal("meaning_transfer", results[0].DimensionKey);
        }

        [Fact]
        public async Task Saving_with_a_nonexistent_exam_type_id_throws_on_the_foreign_key_constraint()
        {
            await using var context = _fixture.CreateContext();
            var repository = new AssessmentDimensionRepository(context);

            await repository.AddAsync(new AssessmentDimension
            {
                Id = Guid.NewGuid(),
                ExamTypeId = Guid.NewGuid(),
                DimensionKey = "meaning_transfer",
                DimensionName = "Meaning transfer",
                ScaleType = ScaleType.band_1_5,
                LevelDescriptions = "{\"1\":\"...\"}",
                RubricVersion = "2024-02",
                EffectiveFrom = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
        }
    }
}
