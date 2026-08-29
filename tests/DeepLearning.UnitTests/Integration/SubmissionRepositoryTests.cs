using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence.Repositories;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.UnitTests.Integration
{
    [Collection(PostgresCollection.Name)]
    public class SubmissionRepositoryTests
    {
        private readonly PostgresContainerFixture _fixture;

        public SubmissionRepositoryTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task A_submission_round_trips_with_its_grading_results_and_error_list_joined_across_tables()
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
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = $"test_{Guid.NewGuid():N}",
                Email = $"{Guid.NewGuid():N}@test.local",
                PasswordHash = "hash",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var question = new Question
            {
                Id = Guid.NewGuid(),
                TaskType = TaskType.A,
                Difficulty = Difficulty.medium,
                Title = "Integration Test Question",
                SourceText = "Original source text.",
                Origin = QuestionOrigin.user_uploaded,
                SourceType = SourceType.user_generated,
                Visibility = Visibility.Private,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var dimension = new AssessmentDimension
            {
                Id = Guid.NewGuid(),
                ExamTypeId = examType.Id,
                DimensionKey = "meaning_transfer",
                DimensionName = "Meaning transfer",
                ScaleType = ScaleType.band_1_5,
                PassThreshold = "Band 2 or above",
                LevelDescriptions = "{\"1\":\"best\",\"2\":\"ok\"}",
                RubricVersion = "2024-02",
                EffectiveFrom = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var taxonomy = new ErrorTaxonomy
            {
                Id = Guid.NewGuid(),
                ExamTypeId = examType.Id,
                CategoryKey = "distortion",
                CategoryName = "Distortion",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await context.ExamTypes.AddAsync(examType);
            await context.Users.AddAsync(user);
            await context.Questions.AddAsync(question);
            await context.AssessmentDimensions.AddAsync(dimension);
            await context.ErrorTaxonomies.AddAsync(taxonomy);
            await context.SaveChangesAsync();

            var repository = new SubmissionRepository(context);
            var submission = new Submission
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                UserId = user.Id,
                TaskType = TaskType.A,
                Content = "\"my translation\"",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            submission.TransitionTo(SubmissionStatus.submitted);
            submission.TransitionTo(SubmissionStatus.grading);
            await repository.AddAsync(submission);
            await context.SaveChangesAsync();

            await repository.AddGradingResultsAsync([
                new GradingResult
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = submission.Id,
                    DimensionId = dimension.Id,
                    RubricVersion = dimension.RubricVersion,
                    Band = 2,
                    PassBool = true,
                    Rationale = "Meets Band 2.",
                    CreatedAt = DateTimeOffset.UtcNow,
                },
            ]);
            await repository.AddErrorListItemsAsync([
                new ErrorListItem
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = submission.Id,
                    ErrorTaxonomyId = taxonomy.Id,
                    DimensionId = dimension.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
            ]);
            submission.TransitionTo(SubmissionStatus.graded);
            await context.SaveChangesAsync();

            await using var readContext = _fixture.CreateContext();
            var readRepository = new SubmissionRepository(readContext);

            var fetchedSubmission = await readRepository.GetByIdAsync(submission.Id);
            var gradingResults = await readRepository.GetGradingResultsAsync(submission.Id);
            var errorList = await readRepository.GetErrorListAsync(submission.Id);

            Assert.NotNull(fetchedSubmission);
            Assert.Equal(SubmissionStatus.graded, fetchedSubmission!.Status);
            Assert.Single(gradingResults);
            Assert.Equal("meaning_transfer", gradingResults[0].Dimension?.DimensionKey);
            Assert.Single(errorList);
            Assert.Equal("distortion", errorList[0].ErrorTaxonomy?.CategoryKey);
        }

        /// <summary>
        /// Design doc §11.2's Step 4 test strategy calls for "模拟中途失败验证整体回滚" — this
        /// proves it at the persistence layer directly, decoupled from whether
        /// GradeSubmissionCommandHandler's own validation would have caught the bad value first
        /// (it now does, for band specifically — see GradeSubmissionCommandHandler.ValidatePayload).
        /// Adds one valid and one CHECK-constraint-violating GradingResult in the SAME
        /// SaveChangesAsync call and confirms neither survives — proving one SaveChangesAsync
        /// call really is one all-or-nothing DB transaction, not "the bad row gets rejected while
        /// the good one commits."
        /// </summary>
        [Fact]
        public async Task A_constraint_violation_in_one_grading_result_rolls_back_the_whole_batch_including_valid_rows()
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
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = $"test_{Guid.NewGuid():N}",
                Email = $"{Guid.NewGuid():N}@test.local",
                PasswordHash = "hash",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var question = new Question
            {
                Id = Guid.NewGuid(),
                TaskType = TaskType.A,
                Difficulty = Difficulty.medium,
                Title = "Integration Test Question",
                SourceText = "Original source text.",
                Origin = QuestionOrigin.user_uploaded,
                SourceType = SourceType.user_generated,
                Visibility = Visibility.Private,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var dimensionA = new AssessmentDimension
            {
                Id = Guid.NewGuid(),
                ExamTypeId = examType.Id,
                DimensionKey = "meaning_transfer",
                DimensionName = "Meaning transfer",
                ScaleType = ScaleType.band_1_5,
                PassThreshold = "Band 2 or above",
                LevelDescriptions = "{\"1\":\"best\",\"2\":\"ok\"}",
                RubricVersion = "2024-02",
                EffectiveFrom = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var dimensionB = new AssessmentDimension
            {
                Id = Guid.NewGuid(),
                ExamTypeId = examType.Id,
                DimensionKey = "textual_norms",
                DimensionName = "Application of textual norms and conventions",
                ScaleType = ScaleType.band_1_5,
                PassThreshold = "Band 3 or above",
                LevelDescriptions = "{\"1\":\"best\",\"2\":\"ok\"}",
                RubricVersion = "2024-02",
                EffectiveFrom = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await context.ExamTypes.AddAsync(examType);
            await context.Users.AddAsync(user);
            await context.Questions.AddAsync(question);
            await context.AssessmentDimensions.AddRangeAsync(dimensionA, dimensionB);
            await context.SaveChangesAsync();

            var repository = new SubmissionRepository(context);
            var submission = new Submission
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                UserId = user.Id,
                TaskType = TaskType.A,
                Content = "\"my translation\"",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            submission.TransitionTo(SubmissionStatus.submitted);
            submission.TransitionTo(SubmissionStatus.grading);
            await repository.AddAsync(submission);
            await context.SaveChangesAsync();

            await repository.AddGradingResultsAsync([
                new GradingResult
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = submission.Id,
                    DimensionId = dimensionA.Id,
                    RubricVersion = dimensionA.RubricVersion,
                    Band = 2, // valid
                    PassBool = true,
                    Rationale = "Meets Band 2.",
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                new GradingResult
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = submission.Id,
                    DimensionId = dimensionB.Id,
                    RubricVersion = dimensionB.RubricVersion,
                    Band = 99, // violates ck_grading_results_band_range
                    PassBool = false,
                    Rationale = "Out of range on purpose.",
                    CreatedAt = DateTimeOffset.UtcNow,
                },
            ]);

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

            await using var readContext = _fixture.CreateContext();
            var readRepository = new SubmissionRepository(readContext);
            var gradingResults = await readRepository.GetGradingResultsAsync(submission.Id);

            Assert.Empty(gradingResults);
        }
    }
}
