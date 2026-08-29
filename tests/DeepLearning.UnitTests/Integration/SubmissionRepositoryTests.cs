using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence.Repositories;
using DeepLearning.UnitTests.TestInfrastructure;

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
    }
}
