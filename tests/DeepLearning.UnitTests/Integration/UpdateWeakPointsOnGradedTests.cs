using DeepLearning.Application.Common;
using DeepLearning.Application.Features.WeakPoints.EventHandlers;
using DeepLearning.Application.Interfaces;
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
    /// Design doc §10.4's recurrence semantics ("学会了又忘了" vs. "从未真正学会" are different
    /// signals) proven against a real Postgres container rather than mocks — same convention as
    /// StandardOverrideRepositoryTests. Uses a fake ISubmissionRepository (only GetErrorListAsync
    /// is exercised by this handler) so the test controls exactly which error category surfaces,
    /// while still seeding real User/Question/Submission rows so weak_points/weak_point_occurrences'
    /// own foreign keys are satisfied.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class UpdateWeakPointsOnGradedTests
    {
        private readonly PostgresContainerFixture _fixture;

        public UpdateWeakPointsOnGradedTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        private class FixedErrorListSubmissionRepository : ISubmissionRepository
        {
            private readonly List<ErrorListItem> _errors;

            public FixedErrorListSubmissionRepository(List<ErrorListItem> errors) => _errors = errors;

            public Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task<List<Submission>> ListByUserAsync(Guid userId, Guid? questionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task<List<GradingResult>> GetGradingResultsAsync(Guid submissionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task<List<ErrorListItem>> GetErrorListAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult(_errors);

            public Task AddAsync(Submission submission, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task AddGradingResultsAsync(IEnumerable<GradingResult> results, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task AddErrorListItemsAsync(IEnumerable<ErrorListItem> items, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private class NoOpPublisher : IPublisher
        {
            public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
                where TNotification : INotification
                => Task.CompletedTask;
        }

        private static User NewUser() => new()
        {
            Id = Guid.NewGuid(),
            Username = $"test_{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        private static Question NewQuestion() => new()
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

        private static Submission NewSubmission(Guid questionId, Guid userId)
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

        private static ErrorListItem NewErrorItem(Guid submissionId, string dimensionName, string categoryName) => new()
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            ErrorTaxonomyId = Guid.NewGuid(),
            DimensionId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            Dimension = new AssessmentDimension { DimensionName = dimensionName },
            ErrorTaxonomy = new ErrorTaxonomy { CategoryName = categoryName },
        };

        private static async Task HandleAsync(AppDbContext context, Guid userId, Guid submissionId, string dimensionName, string categoryName)
        {
            var submissionRepository = new FixedErrorListSubmissionRepository([NewErrorItem(submissionId, dimensionName, categoryName)]);
            var weakPointRepository = new WeakPointRepository(context);
            var unitOfWork = new UnitOfWork(context, new NoOpPublisher());
            var handler = new UpdateWeakPointsOnGraded(submissionRepository, weakPointRepository, unitOfWork);

            var domainEvent = new SubmissionGradedEvent
            {
                SubmissionId = submissionId,
                UserId = userId,
                QuestionId = Guid.NewGuid(),
                ExamTypeId = Guid.NewGuid(),
                TaskType = TaskType.A,
                GradedAt = DateTimeOffset.UtcNow,
            };
            await handler.Handle(new DomainEventNotification<SubmissionGradedEvent>(domainEvent), CancellationToken.None);
        }

        [Fact]
        public async Task First_occurrence_creates_an_active_weak_point_with_no_recurrence()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            const string dimensionName = "Meaning transfer";
            var categoryName = $"Distortion {suffix}";
            var category = $"{dimensionName} - {categoryName}";

            Guid userId, submissionId;
            await using (var context = _fixture.CreateContext())
            {
                var user = NewUser();
                var question = NewQuestion();
                await context.Users.AddAsync(user);
                await context.Questions.AddAsync(question);
                await context.SaveChangesAsync();

                var submission = NewSubmission(question.Id, user.Id);
                await context.Submissions.AddAsync(submission);
                await context.SaveChangesAsync();

                userId = user.Id;
                submissionId = submission.Id;

                await HandleAsync(context, userId, submissionId, dimensionName, categoryName);
            }

            await using var readContext = _fixture.CreateContext();
            var weakPoint = await new WeakPointRepository(readContext).GetByUserAndCategoryAsync(userId, category);

            Assert.NotNull(weakPoint);
            Assert.Equal(WeakPointStatus.active, weakPoint!.Status);
            Assert.Equal(0, weakPoint.RecurrenceCount);

            var occurrence = await readContext.WeakPointOccurrences.SingleAsync(x => x.SubmissionId == submissionId);
            Assert.False(occurrence.IsRecurrence);
        }

        [Fact]
        public async Task A_weak_point_resolved_then_seen_again_is_marked_active_again_as_a_recurrence()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            const string dimensionName = "Meaning transfer";
            var categoryName = $"Distortion {suffix}";
            var category = $"{dimensionName} - {categoryName}";

            Guid userId, firstSubmissionId, secondSubmissionId;
            await using (var context = _fixture.CreateContext())
            {
                var user = NewUser();
                var questionOne = NewQuestion();
                var questionTwo = NewQuestion();
                await context.Users.AddAsync(user);
                await context.Questions.AddRangeAsync(questionOne, questionTwo);
                await context.SaveChangesAsync();

                var firstSubmission = NewSubmission(questionOne.Id, user.Id);
                var secondSubmission = NewSubmission(questionTwo.Id, user.Id);
                await context.Submissions.AddRangeAsync(firstSubmission, secondSubmission);
                await context.SaveChangesAsync();

                userId = user.Id;
                firstSubmissionId = firstSubmission.Id;
                secondSubmissionId = secondSubmission.Id;

                await HandleAsync(context, userId, firstSubmissionId, dimensionName, categoryName);
            }

            // Simulate the weak point having been worked through and marked resolved (there is
            // no automated "resolved" trigger yet in Step 6's scope — this stands in for whatever
            // marks it resolved, matching design doc §10.4's premise that resolution and
            // recurrence are two independently-triggered events).
            await using (var resolveContext = _fixture.CreateContext())
            {
                var weakPoint = await resolveContext.WeakPoints.SingleAsync(x => x.UserId == userId && x.Category == category);
                weakPoint.Status = WeakPointStatus.resolved;
                await resolveContext.SaveChangesAsync();
            }

            await using (var context = _fixture.CreateContext())
            {
                await HandleAsync(context, userId, secondSubmissionId, dimensionName, categoryName);
            }

            await using var readContext = _fixture.CreateContext();
            var final = await readContext.WeakPoints.SingleAsync(x => x.UserId == userId && x.Category == category);
            Assert.Equal(WeakPointStatus.active, final.Status);
            Assert.Equal(1, final.RecurrenceCount);
            Assert.Equal(Priority.high, final.Priority);

            var secondOccurrence = await readContext.WeakPointOccurrences.SingleAsync(x => x.SubmissionId == secondSubmissionId);
            Assert.True(secondOccurrence.IsRecurrence);
        }
    }
}
