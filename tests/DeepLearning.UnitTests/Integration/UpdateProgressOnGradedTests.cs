using DeepLearning.Application.Common;
using DeepLearning.Application.Features.Progress.EventHandlers;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Events;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.Infrastructure.Persistence.Repositories;
using DeepLearning.UnitTests.TestInfrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.UnitTests.Integration
{
    /// <summary>
    /// Design doc §11.2's Step 6 test strategy calls for "各Handler业务逻辑" unit coverage per
    /// subscriber — Api/SubmissionGradedDomainEventTests.cs only ever exercises ONE graded
    /// submission per handler, which can't distinguish "recomputes an average across multiple
    /// submissions correctly" from "just copies the one submission's own band straight through".
    /// This drives UpdateProgressOnGraded across TWO graded submissions for the same
    /// user+difficulty tier and asserts the recomputed average/pass-rate, against a real Postgres
    /// container — same convention as Integration/UpdateWeakPointsOnGradedTests.cs.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class UpdateProgressOnGradedTests
    {
        private readonly PostgresContainerFixture _fixture;

        public UpdateProgressOnGradedTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        private class NoOpPublisher : IPublisher
        {
            public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
                where TNotification : INotification
                => Task.CompletedTask;
        }

        [Fact]
        public async Task Two_graded_submissions_the_same_day_produce_a_recomputed_average_band_and_pass_rate()
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
            await context.ExamTypes.AddAsync(examType);
            await context.Users.AddAsync(user);
            await context.Questions.AddAsync(question);
            await context.AssessmentDimensions.AddAsync(dimension);
            await context.SaveChangesAsync();

            var submissionOne = NewGradedSubmission(question.Id, user.Id);
            var submissionTwo = NewGradedSubmission(question.Id, user.Id);
            await context.Submissions.AddRangeAsync(submissionOne, submissionTwo);
            await context.SaveChangesAsync();

            await context.GradingResults.AddRangeAsync(
                NewGradingResult(submissionOne.Id, dimension.Id, band: 2, passBool: true),
                NewGradingResult(submissionTwo.Id, dimension.Id, band: 4, passBool: false));
            await context.SaveChangesAsync();

            var questionRepository = new QuestionRepository(context);
            var progressRepository = new ProgressRepository(context);
            var unitOfWork = new UnitOfWork(context, new NoOpPublisher());
            var handler = new UpdateProgressOnGraded(questionRepository, progressRepository, unitOfWork);

            var now = DateTimeOffset.UtcNow;
            await handler.Handle(new DomainEventNotification<SubmissionGradedEvent>(new SubmissionGradedEvent
            {
                SubmissionId = submissionOne.Id,
                UserId = user.Id,
                QuestionId = question.Id,
                ExamTypeId = examType.Id,
                TaskType = TaskType.A,
                GradedAt = now,
            }), CancellationToken.None);
            await handler.Handle(new DomainEventNotification<SubmissionGradedEvent>(new SubmissionGradedEvent
            {
                SubmissionId = submissionTwo.Id,
                UserId = user.Id,
                QuestionId = question.Id,
                ExamTypeId = examType.Id,
                TaskType = TaskType.A,
                GradedAt = now,
            }), CancellationToken.None);

            await using var readContext = _fixture.CreateContext();
            var snapshot = await readContext.ProgressSnapshots.SingleAsync(x => x.UserId == user.Id);

            Assert.Equal("medium", snapshot.DifficultyTier);
            // (2 + 4) / 2 = 3.0 — recomputed from source each time, not incremented in place.
            Assert.Equal(3.0m, snapshot.AvgBandMeaningTransfer);
            // Only submissionOne passed (PassBool=true) out of 2 total -> 50%.
            Assert.Equal(50.00m, snapshot.PassRate);
        }

        private static Submission NewGradedSubmission(Guid questionId, Guid userId)
        {
            var submission = new Submission
            {
                Id = Guid.NewGuid(),
                QuestionId = questionId,
                UserId = userId,
                TaskType = TaskType.A,
                Content = "\"my translation\"",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            submission.TransitionTo(SubmissionStatus.submitted);
            submission.TransitionTo(SubmissionStatus.grading);
            submission.TransitionTo(SubmissionStatus.graded);
            return submission;
        }

        private static GradingResult NewGradingResult(Guid submissionId, Guid dimensionId, int band, bool passBool) => new()
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            DimensionId = dimensionId,
            RubricVersion = "2024-02",
            Band = band,
            PassBool = passBool,
            Rationale = "test rationale",
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
