using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence.Repositories;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.UnitTests.Integration
{
    /// <summary>
    /// Self-audit finding (2026-08-30): Submission.TransitionTo's in-memory state-machine check
    /// only guards SEQUENTIAL misuse (a second grade() call arriving after the first already
    /// committed Grading correctly 409s) — it does nothing for two calls that both load the same
    /// Submitted row before either commits, since both see the same in-memory Status and both
    /// pass the check. Fixed via SubmissionConfiguration.UseXminAsConcurrencyToken(). This test
    /// proves the fix deterministically (two real DbContexts racing over the same row) rather than
    /// via Task.WhenAll-style real concurrency, which would make the test's timing non-deterministic.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class SubmissionConcurrencyTests
    {
        private readonly PostgresContainerFixture _fixture;

        public SubmissionConcurrencyTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Two_contexts_racing_to_transition_the_same_submission_to_grading_the_second_save_throws_concurrency_exception()
        {
            Guid submissionId;
            await using (var seedContext = _fixture.CreateContext())
            {
                var examType = new ExamType
                {
                    Id = Guid.NewGuid(),
                    Code = $"test_{Guid.NewGuid():N}",
                    Name = "Concurrency Test Exam Type",
                    SubjectCategory = SubjectCategory.translation,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = $"test_{Guid.NewGuid():N}",
                    Email = $"{Guid.NewGuid():N}@test.local",
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                var question = new Question
                {
                    Id = Guid.NewGuid(),
                    TaskType = TaskType.A,
                    Difficulty = Difficulty.medium,
                    Title = "Concurrency Test Question",
                    SourceText = "Original source text.",
                    Origin = QuestionOrigin.user_uploaded,
                    SourceType = SourceType.user_generated,
                    Visibility = Visibility.Private,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                await seedContext.ExamTypes.AddAsync(examType);
                await seedContext.Users.AddAsync(user);
                await seedContext.Questions.AddAsync(question);
                await seedContext.SaveChangesAsync();

                var repository = new SubmissionRepository(seedContext);
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
                await repository.AddAsync(submission);
                await seedContext.SaveChangesAsync();
                submissionId = submission.Id;
            }

            await using var contextA = _fixture.CreateContext();
            await using var contextB = _fixture.CreateContext();

            // Both "requests" load the same Submitted row before either one writes — the exact
            // race a sequential-only guard can't catch.
            var submissionA = await contextA.Submissions.SingleAsync(x => x.Id == submissionId);
            var submissionB = await contextB.Submissions.SingleAsync(x => x.Id == submissionId);

            submissionA.TransitionTo(SubmissionStatus.grading);
            await contextA.SaveChangesAsync();

            submissionB.TransitionTo(SubmissionStatus.grading); // still legal in-memory — submissionB's own copy is still Submitted
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync());
        }
    }
}
