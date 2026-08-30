using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence.Repositories;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Integration
{
    /// <summary>
    /// Design doc §11.2's Step 5 test strategy calls for "previous_override_id审计链的插入与追溯查询"
    /// and the activation threshold's distinct-question counting — both proven here against a real
    /// Postgres container rather than an in-memory fake, same convention as SubmissionRepositoryTests.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class StandardOverrideRepositoryTests
    {
        private readonly PostgresContainerFixture _fixture;

        public StandardOverrideRepositoryTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
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
            submission.TransitionTo(SubmissionStatus.under_dispute);
            return submission;
        }

        private static FollowUpQuestion NewFollowUp(Guid submissionId, Guid userId) => new()
        {
            Id = Guid.NewGuid(),
            SubmissionId = submissionId,
            UserId = userId,
            QuestionText = "Why was this marked wrong?",
            AiResponse = "You're right, the rubric was misapplied here.",
            Verdict = FollowUpVerdict.user_correct,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        [Fact]
        public async Task GetActiveByRuleAsync_returns_only_the_active_row_ignoring_observing_and_deprecated_ones()
        {
            await using var context = _fixture.CreateContext();
            var repository = new StandardOverrideRepository(context);

            var user = NewUser();
            var question = NewQuestion();
            await context.Users.AddAsync(user);
            await context.Questions.AddAsync(question);
            var submission = NewSubmission(question.Id, user.Id);
            await context.Submissions.AddAsync(submission);
            await context.SaveChangesAsync();

            var followUp = NewFollowUp(submission.Id, user.Id);
            await context.FollowUpQuestions.AddAsync(followUp);
            await context.SaveChangesAsync();

            const string dimensionOrRule = "meaning_transfer";

            var deprecated = new StandardOverride
            {
                Id = Guid.NewGuid(),
                Scope = OverrideScope.grading_rubric,
                DimensionOrRule = dimensionOrRule,
                RevisedRuleText = "old revision, now superseded",
                TriggeredByFollowupId = followUp.Id,
                Status = OverrideStatus.deprecated,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var active = new StandardOverride
            {
                Id = Guid.NewGuid(),
                Scope = OverrideScope.grading_rubric,
                DimensionOrRule = dimensionOrRule,
                RevisedRuleText = "current revision",
                TriggeredByFollowupId = followUp.Id,
                Status = OverrideStatus.active,
                PreviousOverrideId = deprecated.Id,
                EffectiveFrom = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var observingChallenger = new StandardOverride
            {
                Id = Guid.NewGuid(),
                Scope = OverrideScope.grading_rubric,
                DimensionOrRule = dimensionOrRule,
                RevisedRuleText = "candidate next revision",
                TriggeredByFollowupId = followUp.Id,
                Status = OverrideStatus.observing,
                PreviousOverrideId = active.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await repository.AddAsync(deprecated);
            await repository.AddAsync(active);
            await repository.AddAsync(observingChallenger);
            await context.SaveChangesAsync();

            await using var readContext = _fixture.CreateContext();
            var readRepository = new StandardOverrideRepository(readContext);

            var found = await readRepository.GetActiveByRuleAsync(OverrideScope.grading_rubric, dimensionOrRule);

            Assert.NotNull(found);
            Assert.Equal(active.Id, found!.Id);

            // Traceback: the active row's own PreviousOverrideId still points at the deprecated
            // row it superseded, and the newer observing row points at the active row — the
            // insert-only audit chain (design doc §10.6) survives a round trip through the DB.
            var refetchedActive = await readRepository.GetByIdAsync(active.Id);
            Assert.Equal(deprecated.Id, refetchedActive!.PreviousOverrideId);

            var refetchedObserving = await readRepository.GetByIdAsync(observingChallenger.Id);
            Assert.Equal(active.Id, refetchedObserving!.PreviousOverrideId);
        }

        [Fact]
        public async Task CountDistinctQuestionsPendingAsync_counts_each_question_once_even_with_multiple_follow_ups_on_it()
        {
            await using var context = _fixture.CreateContext();
            var repository = new StandardOverrideRepository(context);

            var user = NewUser();
            var questionOne = NewQuestion();
            var questionTwo = NewQuestion();
            await context.Users.AddAsync(user);
            await context.Questions.AddRangeAsync(questionOne, questionTwo);

            // Two submissions/follow-ups on the SAME question (questionOne) plus one on a
            // different question (questionTwo) — design doc §10.6 requires independent
            // confirmation on DIFFERENT questions, so the two on questionOne must count as one.
            var submissionOneA = NewSubmission(questionOne.Id, user.Id);
            var submissionOneB = NewSubmission(questionOne.Id, user.Id);
            var submissionTwo = NewSubmission(questionTwo.Id, user.Id);
            await context.Submissions.AddRangeAsync(submissionOneA, submissionOneB, submissionTwo);
            await context.SaveChangesAsync();

            var followUpOneA = NewFollowUp(submissionOneA.Id, user.Id);
            var followUpOneB = NewFollowUp(submissionOneB.Id, user.Id);
            var followUpTwo = NewFollowUp(submissionTwo.Id, user.Id);
            await context.FollowUpQuestions.AddRangeAsync(followUpOneA, followUpOneB, followUpTwo);
            await context.SaveChangesAsync();

            const string dimensionOrRule = "meaning_transfer";

            var overrideOneA = new StandardOverride
            {
                Id = Guid.NewGuid(),
                Scope = OverrideScope.grading_rubric,
                DimensionOrRule = dimensionOrRule,
                RevisedRuleText = "revision text",
                TriggeredByFollowupId = followUpOneA.Id,
                Status = OverrideStatus.observing,
                PreviousOverrideId = null,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var overrideOneB = new StandardOverride
            {
                Id = Guid.NewGuid(),
                Scope = OverrideScope.grading_rubric,
                DimensionOrRule = dimensionOrRule,
                RevisedRuleText = "revision text",
                TriggeredByFollowupId = followUpOneB.Id,
                Status = OverrideStatus.observing,
                PreviousOverrideId = null,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await repository.AddAsync(overrideOneA);
            await repository.AddAsync(overrideOneB);
            await context.SaveChangesAsync();

            await using var midContext = _fixture.CreateContext();
            var midRepository = new StandardOverrideRepository(midContext);
            var countBeforeSecondQuestion = await midRepository.CountDistinctQuestionsPendingAsync(OverrideScope.grading_rubric, dimensionOrRule, null);
            Assert.Equal(1, countBeforeSecondQuestion);

            var overrideTwo = new StandardOverride
            {
                Id = Guid.NewGuid(),
                Scope = OverrideScope.grading_rubric,
                DimensionOrRule = dimensionOrRule,
                RevisedRuleText = "revision text",
                TriggeredByFollowupId = followUpTwo.Id,
                Status = OverrideStatus.observing,
                PreviousOverrideId = null,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await midRepository.AddAsync(overrideTwo);
            await midContext.SaveChangesAsync();

            await using var readContext = _fixture.CreateContext();
            var readRepository = new StandardOverrideRepository(readContext);
            var countAfterSecondQuestion = await readRepository.CountDistinctQuestionsPendingAsync(OverrideScope.grading_rubric, dimensionOrRule, null);
            Assert.Equal(2, countAfterSecondQuestion);
        }
    }
}
