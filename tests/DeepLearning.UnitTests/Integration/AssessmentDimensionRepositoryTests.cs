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

        /// <summary>
        /// Self-audit fix (2026-08-31, design doc §10.1 rubric versioning): before this,
        /// ListByExamTypeAsync ignored EffectiveFrom/EffectiveTo entirely, so two versions of the
        /// same dimension_key would both load and GradeSubmissionCommandHandler's
        /// dimensions.ToDictionary(x => x.DimensionKey) would throw ArgumentException on the very
        /// next grading call. Proves the fix directly at the repository level: a closed-out
        /// (EffectiveTo in the past) row and a not-yet-started (EffectiveFrom in the future) row
        /// are both excluded, only the currently-effective one loads.
        /// </summary>
        [Fact]
        public async Task ListByExamTypeAsync_only_returns_the_version_currently_within_its_effective_window()
        {
            var examType = await CreateExamTypeAsync();
            var now = DateTimeOffset.UtcNow;

            await using var context = _fixture.CreateContext();
            var repository = new AssessmentDimensionRepository(context);
            var closedOut = new AssessmentDimension
            {
                Id = Guid.NewGuid(),
                ExamTypeId = examType.Id,
                DimensionKey = "meaning_transfer",
                DimensionName = "Meaning transfer (old)",
                ScaleType = ScaleType.band_1_5,
                LevelDescriptions = "{\"1\":\"...\"}",
                RubricVersion = "2024-01",
                EffectiveFrom = now.AddDays(-30),
                EffectiveTo = now.AddDays(-1),
                CreatedAt = now,
            };
            var current = new AssessmentDimension
            {
                Id = Guid.NewGuid(),
                ExamTypeId = examType.Id,
                DimensionKey = "meaning_transfer",
                DimensionName = "Meaning transfer",
                ScaleType = ScaleType.band_1_5,
                LevelDescriptions = "{\"1\":\"...\"}",
                RubricVersion = "2024-02",
                EffectiveFrom = now.AddDays(-1),
                CreatedAt = now,
            };
            var notYetEffective = new AssessmentDimension
            {
                Id = Guid.NewGuid(),
                ExamTypeId = examType.Id,
                DimensionKey = "meaning_transfer",
                DimensionName = "Meaning transfer (future)",
                ScaleType = ScaleType.band_1_5,
                LevelDescriptions = "{\"1\":\"...\"}",
                RubricVersion = "2024-03",
                EffectiveFrom = now.AddDays(30),
                CreatedAt = now,
            };
            await repository.AddAsync(closedOut);
            await repository.AddAsync(current);
            await repository.AddAsync(notYetEffective);
            await context.SaveChangesAsync();

            await using var readContext = _fixture.CreateContext();
            var readRepository = new AssessmentDimensionRepository(readContext);
            var results = await readRepository.ListByExamTypeAsync(examType.Id, null);

            var match = Assert.Single(results, x => x.DimensionKey == "meaning_transfer");
            Assert.Equal("2024-02", match.RubricVersion);
        }

        [Fact]
        public async Task ListOpenEndedByKeyAsync_excludes_a_version_that_has_already_been_closed_out()
        {
            var examType = await CreateExamTypeAsync();
            var now = DateTimeOffset.UtcNow;

            await using var context = _fixture.CreateContext();
            var repository = new AssessmentDimensionRepository(context);
            var closed = new AssessmentDimension
            {
                Id = Guid.NewGuid(),
                ExamTypeId = examType.Id,
                DimensionKey = "textual_norms",
                DimensionName = "Textual norms",
                ScaleType = ScaleType.band_1_5,
                LevelDescriptions = "{\"1\":\"...\"}",
                RubricVersion = "2024-01",
                EffectiveFrom = now.AddDays(-10),
                EffectiveTo = now,
                CreatedAt = now,
            };
            var open = new AssessmentDimension
            {
                Id = Guid.NewGuid(),
                ExamTypeId = examType.Id,
                DimensionKey = "textual_norms",
                DimensionName = "Textual norms",
                ScaleType = ScaleType.band_1_5,
                LevelDescriptions = "{\"1\":\"...\"}",
                RubricVersion = "2024-02",
                EffectiveFrom = now,
                CreatedAt = now,
            };
            await repository.AddAsync(closed);
            await repository.AddAsync(open);
            await context.SaveChangesAsync();

            await using var readContext = _fixture.CreateContext();
            var readRepository = new AssessmentDimensionRepository(readContext);
            var results = await readRepository.ListOpenEndedByKeyAsync(examType.Id, "textual_norms");

            var match = Assert.Single(results);
            Assert.Equal("2024-02", match.RubricVersion);
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
