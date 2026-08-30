using DeepLearning.Application.Common;
using DeepLearning.Application.Features.ReviewLibrary.EventHandlers;
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
    /// subscriber — Api/SubmissionGradedDomainEventTests.cs and
    /// Api/DeepLearningContentControllerTests.cs both only ever fire this handler ONCE per
    /// user+question, which proves the create-a-new-review-row branch but never the
    /// already-reviewed increment branch. This drives ExtractKnowledgePointsOnGraded across TWO
    /// grading events for the same user+question and asserts TimesEncountered goes 1 -> 2,
    /// against a real Postgres container — same convention as
    /// Integration/UpdateWeakPointsOnGradedTests.cs.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class ExtractKnowledgePointsOnGradedTests
    {
        private readonly PostgresContainerFixture _fixture;

        public ExtractKnowledgePointsOnGradedTests(PostgresContainerFixture fixture)
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
        public async Task A_second_grading_event_for_the_same_user_and_question_increments_times_encountered_instead_of_duplicating_the_review_row()
        {
            await using var context = _fixture.CreateContext();

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
            var pattern = new SentencePattern
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                PatternName = "Cleft sentence",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var vocab = new VocabExpression
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                EnglishExpr = "in light of",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await context.Users.AddAsync(user);
            await context.Questions.AddAsync(question);
            await context.SentencePatterns.AddAsync(pattern);
            await context.VocabExpressions.AddAsync(vocab);
            await context.SaveChangesAsync();

            var reviewLibraryRepository = new ReviewLibraryRepository(context);
            var unitOfWork = new UnitOfWork(context, new NoOpPublisher());
            var handler = new ExtractKnowledgePointsOnGraded(reviewLibraryRepository, unitOfWork);

            var firstEvent = new DomainEventNotification<SubmissionGradedEvent>(new SubmissionGradedEvent
            {
                SubmissionId = Guid.NewGuid(),
                UserId = user.Id,
                QuestionId = question.Id,
                ExamTypeId = Guid.NewGuid(),
                TaskType = TaskType.A,
                GradedAt = DateTimeOffset.UtcNow,
            });
            await handler.Handle(firstEvent, CancellationToken.None);

            await using (var midContext = _fixture.CreateContext())
            {
                var patternReview = await midContext.UserPatternReview.SingleAsync(x => x.UserId == user.Id && x.PatternId == pattern.Id);
                Assert.Equal(1, patternReview.TimesEncountered);
                var vocabReview = await midContext.UserVocabReview.SingleAsync(x => x.UserId == user.Id && x.VocabId == vocab.Id);
                Assert.Equal(1, vocabReview.TimesEncountered);
            }

            // A second, independent submission graded against the same question by the same user.
            await using (var secondContext = _fixture.CreateContext())
            {
                var secondHandler = new ExtractKnowledgePointsOnGraded(
                    new ReviewLibraryRepository(secondContext), new UnitOfWork(secondContext, new NoOpPublisher()));

                var secondEvent = new DomainEventNotification<SubmissionGradedEvent>(new SubmissionGradedEvent
                {
                    SubmissionId = Guid.NewGuid(),
                    UserId = user.Id,
                    QuestionId = question.Id,
                    ExamTypeId = Guid.NewGuid(),
                    TaskType = TaskType.A,
                    GradedAt = DateTimeOffset.UtcNow,
                });
                await secondHandler.Handle(secondEvent, CancellationToken.None);
            }

            await using var readContext = _fixture.CreateContext();
            var finalPatternReview = await readContext.UserPatternReview.SingleAsync(x => x.UserId == user.Id && x.PatternId == pattern.Id);
            Assert.Equal(2, finalPatternReview.TimesEncountered);
            var finalVocabReview = await readContext.UserVocabReview.SingleAsync(x => x.UserId == user.Id && x.VocabId == vocab.Id);
            Assert.Equal(2, finalVocabReview.TimesEncountered);
        }
    }
}
