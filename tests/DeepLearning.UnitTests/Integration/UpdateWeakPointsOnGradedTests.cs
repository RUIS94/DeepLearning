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
    /// StandardOverrideRepositoryTests. Unlike the pre-catalog version this now seeds a real
    /// exam_type + dimension + error_taxonomy + error_list + grading_result graph, because the
    /// handler ties each WeakPointOccurrence back to a real ErrorListItem row (FK) and reads the
    /// dimension's band. No weak_point_catalog rows are seeded, so every error falls back to the
    /// legacy "{DimensionName} - {ErrorCategoryName}" bucket — that fallback path is what these
    /// two tests exercise.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class UpdateWeakPointsOnGradedTests
    {
        private readonly PostgresContainerFixture _fixture;

        public UpdateWeakPointsOnGradedTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        private class FixedSubmissionRepository : ISubmissionRepository
        {
            private readonly List<ErrorListItem> _errors;
            private readonly List<GradingResult> _gradingResults;
            private readonly List<Submission> _userSubmissions;

            public FixedSubmissionRepository(
                List<ErrorListItem> errors,
                List<GradingResult> gradingResults,
                List<Submission>? userSubmissions = null)
            {
                _errors = errors;
                _gradingResults = gradingResults;
                _userSubmissions = userSubmissions ?? [];
            }

            public Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task<List<Submission>> ListByUserAsync(Guid userId, Guid? questionId, CancellationToken cancellationToken = default)
                => Task.FromResult(_userSubmissions);

            public Task<List<GradingResult>> GetGradingResultsAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult(_gradingResults);

            public Task<List<ErrorListItem>> GetErrorListAsync(Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult(_errors);

            public Task AddAsync(Submission submission, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task AddGradingResultsAsync(IEnumerable<GradingResult> results, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task AddErrorListItemsAsync(IEnumerable<ErrorListItem> items, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private class EmptyCatalogRepository : IWeakPointCatalogRepository
        {
            public Task<List<WeakPointCatalog>> ListByExamTypeAsync(Guid examTypeId, CancellationToken cancellationToken = default)
                => Task.FromResult(new List<WeakPointCatalog>());
        }

        // No weak_point_classification template configured -> the real classifier returns empty
        // and the rule handles everything. This stand-in reproduces exactly that path.
        private class NoOpWeakPointClassifier : IWeakPointClassifier
        {
            public Task<IReadOnlyDictionary<Guid, Guid>> ClassifyAsync(
                Guid examTypeId,
                IReadOnlyList<WeakPointClassifierError> errors,
                IReadOnlyList<WeakPointCatalog> catalog,
                CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(new Dictionary<Guid, Guid>());
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

        private static ExamType NewExamType() => new()
        {
            Id = Guid.NewGuid(),
            Code = $"exam_{Guid.NewGuid():N}"[..20],
            Name = "Integration Test Exam",
            SubjectCategory = SubjectCategory.translation,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        private static AssessmentDimension NewDimension(Guid examTypeId, string dimensionName) => new()
        {
            Id = Guid.NewGuid(),
            ExamTypeId = examTypeId,
            DimensionKey = $"dim_{Guid.NewGuid():N}"[..16],
            DimensionName = dimensionName,
            ScaleType = ScaleType.band_1_5,
            PassThreshold = "Band 2 or above",
            LevelDescriptions = "{\"1\":\"a\",\"2\":\"b\",\"3\":\"c\",\"4\":\"d\",\"5\":\"e\"}",
            RubricVersion = "2024-02",
            EffectiveFrom = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        private static ErrorTaxonomy NewTaxonomy(Guid examTypeId, string categoryName) => new()
        {
            Id = Guid.NewGuid(),
            ExamTypeId = examTypeId,
            CategoryKey = $"cat_{Guid.NewGuid():N}"[..16],
            CategoryName = categoryName,
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

        /// <summary>
        /// Seeds the full real graph one graded submission needs (exam type, dimension, taxonomy,
        /// one persisted error_list row + its grading_result) and runs the handler against it.
        /// Returns the exam type id so the caller can reuse it across submissions.
        /// </summary>
        private async Task<Guid> RunHandlerAsync(
            Guid userId, Guid questionId, Guid submissionId, Guid examTypeId,
            Guid dimensionId, Guid taxonomyId, int band,
            List<Submission>? userSubmissions = null)
        {
            await using var context = _fixture.CreateContext();

            var error = new ErrorListItem
            {
                Id = Guid.NewGuid(),
                SubmissionId = submissionId,
                ErrorTaxonomyId = taxonomyId,
                DimensionId = dimensionId,
                PositionRef = "para 1",
                SourceTextSnippet = "source snippet",
                UserTextSnippet = "user snippet",
                Explanation = "explanation text",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await context.ErrorList.AddAsync(error);

            var gradingResult = new GradingResult
            {
                Id = Guid.NewGuid(),
                SubmissionId = submissionId,
                DimensionId = dimensionId,
                RubricVersion = "2024-02",
                Band = band,
                PassBool = band <= 2,
                Rationale = "rationale",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await context.GradingResults.AddAsync(gradingResult);
            await context.SaveChangesAsync();

            // Re-read with navigations, matching what SubmissionRepository.GetErrorListAsync /
            // GetGradingResultsAsync return in production.
            var errors = await context.ErrorList
                .Where(x => x.SubmissionId == submissionId)
                .Include(x => x.Dimension)
                .Include(x => x.ErrorTaxonomy)
                .ToListAsync();
            var gradingResults = await context.GradingResults
                .Where(x => x.SubmissionId == submissionId)
                .Include(x => x.Dimension)
                .ToListAsync();

            var handler = new UpdateWeakPointsOnGraded(
                new FixedSubmissionRepository(errors, gradingResults, userSubmissions),
                new WeakPointRepository(context),
                new EmptyCatalogRepository(),
                new NoOpWeakPointClassifier(),
                new UnitOfWork(context, new NoOpPublisher()));

            var domainEvent = new SubmissionGradedEvent
            {
                SubmissionId = submissionId,
                UserId = userId,
                QuestionId = questionId,
                ExamTypeId = examTypeId,
                TaskType = TaskType.A,
                GradedAt = DateTimeOffset.UtcNow,
            };
            await handler.Handle(new DomainEventNotification<SubmissionGradedEvent>(domainEvent), CancellationToken.None);
            return examTypeId;
        }

        [Fact]
        public async Task First_occurrence_creates_an_active_weak_point_with_no_recurrence()
        {
            const string dimensionName = "Meaning transfer";
            var categoryName = $"Distortion {Guid.NewGuid().ToString("N")[..8]}";
            var category = $"{dimensionName} - {categoryName}";

            Guid userId, submissionId, examTypeId, dimensionId, taxonomyId, questionId;
            await using (var context = _fixture.CreateContext())
            {
                var user = NewUser();
                var examType = NewExamType();
                var dimension = NewDimension(examType.Id, dimensionName);
                var taxonomy = NewTaxonomy(examType.Id, categoryName);
                var question = NewQuestion();
                await context.Users.AddAsync(user);
                await context.ExamTypes.AddAsync(examType);
                await context.AssessmentDimensions.AddAsync(dimension);
                await context.ErrorTaxonomies.AddAsync(taxonomy);
                await context.Questions.AddAsync(question);
                await context.SaveChangesAsync();

                var submission = NewSubmission(question.Id, user.Id);
                await context.Submissions.AddAsync(submission);
                await context.SaveChangesAsync();

                userId = user.Id;
                submissionId = submission.Id;
                examTypeId = examType.Id;
                dimensionId = dimension.Id;
                taxonomyId = taxonomy.Id;
                questionId = question.Id;
            }

            await RunHandlerAsync(userId, questionId, submissionId, examTypeId, dimensionId, taxonomyId, band: 3);

            await using var readContext = _fixture.CreateContext();
            var weakPoint = await new WeakPointRepository(readContext).GetByUserAndCategoryAsync(userId, category);

            Assert.NotNull(weakPoint);
            Assert.Equal(WeakPointStatus.active, weakPoint!.Status);
            Assert.Equal(0, weakPoint.RecurrenceCount);
            Assert.Null(weakPoint.CatalogId);
            Assert.Equal(examTypeId, weakPoint.ExamTypeId);

            var occurrence = await readContext.WeakPointOccurrences.SingleAsync(x => x.SubmissionId == submissionId);
            Assert.False(occurrence.IsRecurrence);
            Assert.NotNull(occurrence.ErrorListId);
            Assert.Equal("user snippet", occurrence.Snippet);
            Assert.Equal(3, occurrence.DetectedBand);
        }

        [Fact]
        public async Task A_weak_point_resolved_then_seen_again_is_marked_active_again_as_a_recurrence()
        {
            const string dimensionName = "Meaning transfer";
            var categoryName = $"Distortion {Guid.NewGuid().ToString("N")[..8]}";
            var category = $"{dimensionName} - {categoryName}";

            Guid userId, firstSubmissionId, secondSubmissionId, examTypeId, dimensionId, taxonomyId, questionOneId, questionTwoId;
            await using (var context = _fixture.CreateContext())
            {
                var user = NewUser();
                var examType = NewExamType();
                var dimension = NewDimension(examType.Id, dimensionName);
                var taxonomy = NewTaxonomy(examType.Id, categoryName);
                var questionOne = NewQuestion();
                var questionTwo = NewQuestion();
                await context.Users.AddAsync(user);
                await context.ExamTypes.AddAsync(examType);
                await context.AssessmentDimensions.AddAsync(dimension);
                await context.ErrorTaxonomies.AddAsync(taxonomy);
                await context.Questions.AddRangeAsync(questionOne, questionTwo);
                await context.SaveChangesAsync();

                var firstSubmission = NewSubmission(questionOne.Id, user.Id);
                var secondSubmission = NewSubmission(questionTwo.Id, user.Id);
                await context.Submissions.AddRangeAsync(firstSubmission, secondSubmission);
                await context.SaveChangesAsync();

                userId = user.Id;
                firstSubmissionId = firstSubmission.Id;
                secondSubmissionId = secondSubmission.Id;
                examTypeId = examType.Id;
                dimensionId = dimension.Id;
                taxonomyId = taxonomy.Id;
                questionOneId = questionOne.Id;
                questionTwoId = questionTwo.Id;
            }

            await RunHandlerAsync(userId, questionOneId, firstSubmissionId, examTypeId, dimensionId, taxonomyId, band: 3);

            // Simulate the weak point having been worked through and marked resolved (Step 6 has
            // no automated "resolved" trigger yet — this stands in for whatever marks it so).
            await using (var resolveContext = _fixture.CreateContext())
            {
                var weakPoint = await resolveContext.WeakPoints.SingleAsync(x => x.UserId == userId && x.Category == category);
                weakPoint.Status = WeakPointStatus.resolved;
                weakPoint.ResolvedAt = DateTimeOffset.UtcNow;
                await resolveContext.SaveChangesAsync();
            }

            await RunHandlerAsync(userId, questionTwoId, secondSubmissionId, examTypeId, dimensionId, taxonomyId, band: 3);

            await using var readContext = _fixture.CreateContext();
            var final = await readContext.WeakPoints.SingleAsync(x => x.UserId == userId && x.Category == category);
            Assert.Equal(WeakPointStatus.active, final.Status);
            Assert.Equal(1, final.RecurrenceCount);
            Assert.Equal(Priority.high, final.Priority);
            Assert.Null(final.ResolvedAt);

            var secondOccurrence = await readContext.WeakPointOccurrences.SingleAsync(x => x.SubmissionId == secondSubmissionId);
            Assert.True(secondOccurrence.IsRecurrence);
        }

        [Fact]
        public async Task An_active_weak_point_not_seen_in_the_last_five_graded_submissions_is_resolved()
        {
            const string dimensionName = "Language proficiency";
            var categoryName = $"Grammar {Guid.NewGuid().ToString("N")[..8]}";
            var staleCategory = $"Meaning transfer - Stale {Guid.NewGuid().ToString("N")[..8]}";

            Guid userId, submissionId, examTypeId, dimensionId, taxonomyId, questionId;
            await using (var context = _fixture.CreateContext())
            {
                var user = NewUser();
                var examType = NewExamType();
                var dimension = NewDimension(examType.Id, dimensionName);
                var taxonomy = NewTaxonomy(examType.Id, categoryName);
                var question = NewQuestion();
                await context.Users.AddAsync(user);
                await context.ExamTypes.AddAsync(examType);
                await context.AssessmentDimensions.AddAsync(dimension);
                await context.ErrorTaxonomies.AddAsync(taxonomy);
                await context.Questions.AddAsync(question);
                await context.SaveChangesAsync();

                var submission = NewSubmission(question.Id, user.Id);
                await context.Submissions.AddAsync(submission);

                // A weak point last seen a month ago that this submission does NOT touch.
                await context.WeakPoints.AddAsync(new WeakPoint
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    ExamTypeId = examType.Id,
                    Category = staleCategory,
                    Description = "stale",
                    DetectionSource = "rule",
                    FirstDetectedAt = DateTimeOffset.UtcNow.AddDays(-40),
                    LastSeenAt = DateTimeOffset.UtcNow.AddDays(-30),
                    Status = WeakPointStatus.active,
                    Priority = Priority.medium,
                });
                await context.SaveChangesAsync();

                userId = user.Id;
                submissionId = submission.Id;
                examTypeId = examType.Id;
                dimensionId = dimension.Id;
                taxonomyId = taxonomy.Id;
                questionId = question.Id;
            }

            // Fake "user's graded submissions" — five recent ones, so the cutoff (5th most
            // recent) is a day ago and the stale weak point (last seen 30 days ago) is behind it.
            var recentGraded = Enumerable.Range(0, 6)
                .Select(i => new Submission
                {
                    Id = Guid.NewGuid(),
                    QuestionId = Guid.NewGuid(),
                    UserId = userId,
                    TaskType = TaskType.A,
                    Content = "\"x\"",
                    Status = SubmissionStatus.graded,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-i),
                    UpdatedAt = DateTimeOffset.UtcNow.AddDays(-i),
                })
                .ToList();

            await RunHandlerAsync(userId, questionId, submissionId, examTypeId, dimensionId, taxonomyId, band: 2, userSubmissions: recentGraded);

            await using var readContext = _fixture.CreateContext();
            var stale = await readContext.WeakPoints.SingleAsync(x => x.UserId == userId && x.Category == staleCategory);
            Assert.Equal(WeakPointStatus.resolved, stale.Status);
            Assert.NotNull(stale.ResolvedAt);

            // The weak point this submission's own error created is still active.
            var fresh = await readContext.WeakPoints.SingleAsync(x => x.UserId == userId && x.Category == $"{dimensionName} - {categoryName}");
            Assert.Equal(WeakPointStatus.active, fresh.Status);
        }
    }
}
