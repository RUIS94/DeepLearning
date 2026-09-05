using DeepLearning.Application.Common;
using DeepLearning.Application.Features.Progress.Commands.GenerateProgressTrendSnapshot;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Ai;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.Infrastructure.Persistence.Repositories;
using DeepLearning.UnitTests.Api;
using DeepLearning.UnitTests.TestInfrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepLearning.UnitTests.Integration
{
    /// <summary>
    /// Design doc §11.2 Step 9: GenerateProgressTrendSnapshotCommandHandler recomputes one
    /// (user, difficulty tier, week) progress_snapshots row from real grading_results (the same
    /// ProgressSnapshotCalculator logic Step 6's UpdateProgressOnGraded uses for "today") and
    /// makes an AI call to narrate a trend against prior weeks' history. Assembled by hand against
    /// a real Postgres container, same convention as Integration/UpdateProgressOnGradedTests.cs —
    /// no mocking library in this codebase, hand-rolled fakes only (tests/.../Api/FakeLlmClient.cs).
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class GenerateProgressTrendSnapshotCommandHandlerTests
    {
        private readonly PostgresContainerFixture _fixture;

        private const string MarkerTemplate =
            "PROGRESS_TREND_MARKER current_pass_rate={{ current.pass_rate }} history_count={{ history.size }}";

        public GenerateProgressTrendSnapshotCommandHandlerTests(PostgresContainerFixture fixture)
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

        private static GenerateProgressTrendSnapshotCommandHandler BuildHandler(
            AppDbContext context, ILlmClientResolver llmClientResolver)
        {
            var unitOfWork = new UnitOfWork(context, new NoOpPublisher());
            return new GenerateProgressTrendSnapshotCommandHandler(
                new ExamTypeRepository(context),
                new ProgressRepository(context),
                new AiCallLogRepository(context),
                new ExamConfigLoader(new ExamTypeRepository(context), new PromptTemplateRepository(context), new PromptRenderer()),
                llmClientResolver,
                new AiCallRetryExecutor(TimeSpan.FromMilliseconds(1)),
                unitOfWork,
                NullLogger<GenerateProgressTrendSnapshotCommandHandler>.Instance);
        }

        private async Task<(ExamType ExamType, User User, Question Question, AssessmentDimension Dimension)> SeedBaseDataAsync(AppDbContext context)
        {
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
            await context.PromptTemplates.AddAsync(new PromptTemplate
            {
                Id = Guid.NewGuid(),
                SubjectCategory = SubjectCategory.translation,
                TemplateType = AiOperationType.progress_trend,
                Layer = TemplateLayer.shared_methodology,
                TemplateContent = MarkerTemplate,
                Version = 1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();

            return (examType, user, question, dimension);
        }

        private static async Task SeedGradedSubmissionAsync(
            AppDbContext context, Guid questionId, Guid userId, Guid dimensionId, DateTimeOffset gradedAt, int band, bool passBool)
        {
            var submission = new Submission
            {
                Id = Guid.NewGuid(),
                QuestionId = questionId,
                UserId = userId,
                TaskType = TaskType.A,
                Content = "\"translation\"",
                CreatedAt = gradedAt,
                UpdatedAt = gradedAt,
            };
            submission.TransitionTo(SubmissionStatus.submitted);
            submission.TransitionTo(SubmissionStatus.grading);
            submission.TransitionTo(SubmissionStatus.graded);
            // TransitionTo stamps UpdatedAt = UtcNow — overridden back to the historical date the
            // test actually wants this submission attributed to.
            submission.UpdatedAt = gradedAt;
            await context.Submissions.AddAsync(submission);
            await context.SaveChangesAsync();

            await context.GradingResults.AddAsync(new GradingResult
            {
                Id = Guid.NewGuid(),
                SubmissionId = submission.Id,
                DimensionId = dimensionId,
                RubricVersion = "2024-02",
                Band = band,
                PassBool = passBool,
                Rationale = "test rationale",
                CreatedAt = gradedAt,
            });
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task No_grading_activity_in_the_period_is_skipped_without_creating_a_row_or_calling_the_ai()
        {
            await using var context = _fixture.CreateContext();
            var (examType, user, _, _) = await SeedBaseDataAsync(context);

            var fakeClient = new FakeProgressTrendLlmClient();
            var handler = BuildHandler(context, LlmClientResolverSubstitute.Returning(fakeClient));

            var result = await handler.Handle(
                new GenerateProgressTrendSnapshotCommand(
                    user.Id, examType.Id, "medium", new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 23)),
                CancellationToken.None);

            Assert.True(result.Skipped);
            Assert.Null(result.SnapshotId);
            Assert.Equal(0, fakeClient.CallCount);

            await using var readContext = _fixture.CreateContext();
            Assert.False(await readContext.ProgressSnapshots.AnyAsync(x => x.UserId == user.Id));
        }

        [Fact]
        public async Task Recomputes_the_weekly_aggregate_and_generates_an_ai_trend_note_using_prior_week_history()
        {
            await using var context = _fixture.CreateContext();
            var (examType, user, question, dimension) = await SeedBaseDataAsync(context);

            var week1 = (Start: new DateOnly(2026, 8, 17), End: new DateOnly(2026, 8, 23));
            var week2 = (Start: new DateOnly(2026, 8, 24), End: new DateOnly(2026, 8, 30));

            await SeedGradedSubmissionAsync(
                context, question.Id, user.Id, dimension.Id,
                gradedAt: new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero), band: 4, passBool: false);

            var fakeClient = new FakeProgressTrendLlmClient();
            var handler = BuildHandler(context, LlmClientResolverSubstitute.Returning(fakeClient));

            var week1Result = await handler.Handle(
                new GenerateProgressTrendSnapshotCommand(user.Id, examType.Id, "medium", week1.Start, week1.End),
                CancellationToken.None);

            Assert.False(week1Result.Skipped);
            Assert.True(week1Result.TrendNoteGenerated);
            Assert.Equal(1, fakeClient.CallCount);
            // No history yet for week 1 — the marker template renders history.size as 0.
            Assert.Contains("history_count=0", fakeClient.CapturedPrompts[0]);
            Assert.Contains("current_pass_rate=0", fakeClient.CapturedPrompts[0]);

            await SeedGradedSubmissionAsync(
                context, question.Id, user.Id, dimension.Id,
                gradedAt: new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero), band: 2, passBool: true);

            var week2Result = await handler.Handle(
                new GenerateProgressTrendSnapshotCommand(user.Id, examType.Id, "medium", week2.Start, week2.End),
                CancellationToken.None);

            Assert.False(week2Result.Skipped);
            Assert.True(week2Result.TrendNoteGenerated);
            Assert.Equal(2, fakeClient.CallCount);
            // Week 1's already-persisted snapshot is now history for week 2's call.
            Assert.Contains("history_count=1", fakeClient.CapturedPrompts[1]);
            Assert.Contains("current_pass_rate=100", fakeClient.CapturedPrompts[1]);

            await using var readContext = _fixture.CreateContext();
            var snapshots = await readContext.ProgressSnapshots
                .Where(x => x.UserId == user.Id)
                .OrderBy(x => x.PeriodStart)
                .ToListAsync();

            Assert.Equal(2, snapshots.Count);
            Assert.Equal(4.0m, snapshots[0].AvgBandMeaningTransfer);
            Assert.Equal(0.00m, snapshots[0].PassRate);
            Assert.Equal(FakeProgressTrendLlmClient.TrendNote, snapshots[0].TrendNote);
            Assert.Equal(2.0m, snapshots[1].AvgBandMeaningTransfer);
            Assert.Equal(100.00m, snapshots[1].PassRate);
            Assert.Equal(FakeProgressTrendLlmClient.TrendNote, snapshots[1].TrendNote);
        }

        [Fact]
        public async Task Re_running_an_already_narrated_week_with_unchanged_data_does_not_call_the_ai_again()
        {
            // ProgressSnapshotJob re-sends every one of its trailing 12 weeks on every weekly run
            // (see its own doc comment) — without this idempotency check, a week whose grading
            // data hasn't changed since it was last narrated would get a fresh, paid AI call every
            // single run, forever. This test is the regression guard for that bug.
            await using var context = _fixture.CreateContext();
            var (examType, user, question, dimension) = await SeedBaseDataAsync(context);

            await SeedGradedSubmissionAsync(
                context, question.Id, user.Id, dimension.Id,
                gradedAt: new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero), band: 3, passBool: true);

            var fakeClient = new FakeProgressTrendLlmClient();
            var handler = BuildHandler(context, LlmClientResolverSubstitute.Returning(fakeClient));
            var command = new GenerateProgressTrendSnapshotCommand(
                user.Id, examType.Id, "medium", new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 23));

            var firstRun = await handler.Handle(command, CancellationToken.None);
            Assert.True(firstRun.TrendNoteGenerated);
            Assert.Equal(1, fakeClient.CallCount);

            // Same week, same underlying grading data, no new submissions — as if the weekly
            // job's next run re-sent this same already-fully-processed historical week again.
            var secondRun = await handler.Handle(command, CancellationToken.None);
            Assert.False(secondRun.Skipped);
            Assert.True(secondRun.TrendNoteGenerated);
            Assert.Equal(firstRun.SnapshotId, secondRun.SnapshotId);
            // The whole point: no second AI call was made for unchanged, already-narrated data.
            Assert.Equal(1, fakeClient.CallCount);

            await using var readContext = _fixture.CreateContext();
            var snapshot = await readContext.ProgressSnapshots.SingleAsync(x => x.Id == firstRun.SnapshotId);
            Assert.Equal(FakeProgressTrendLlmClient.TrendNote, snapshot.TrendNote);
        }

        [Fact]
        public async Task A_new_submission_landing_in_an_already_narrated_week_triggers_re_narration()
        {
            // The counterpart to the idempotency test above: if a week's numbers genuinely change
            // (e.g. a late-graded submission), the handler must not treat it as "unchanged" —
            // otherwise a real trend change would silently never reach the AI narrative again.
            await using var context = _fixture.CreateContext();
            var (examType, user, question, dimension) = await SeedBaseDataAsync(context);

            await SeedGradedSubmissionAsync(
                context, question.Id, user.Id, dimension.Id,
                gradedAt: new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero), band: 4, passBool: false);

            var fakeClient = new FakeProgressTrendLlmClient();
            var handler = BuildHandler(context, LlmClientResolverSubstitute.Returning(fakeClient));
            var command = new GenerateProgressTrendSnapshotCommand(
                user.Id, examType.Id, "medium", new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 23));

            await handler.Handle(command, CancellationToken.None);
            Assert.Equal(1, fakeClient.CallCount);

            // A second, better-scoring submission lands in the same week — the aggregate changes.
            await SeedGradedSubmissionAsync(
                context, question.Id, user.Id, dimension.Id,
                gradedAt: new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero), band: 2, passBool: true);

            var rerun = await handler.Handle(command, CancellationToken.None);
            Assert.True(rerun.TrendNoteGenerated);
            Assert.Equal(2, fakeClient.CallCount);

            await using var readContext = _fixture.CreateContext();
            var snapshot = await readContext.ProgressSnapshots.SingleAsync(x => x.Id == rerun.SnapshotId);
            // (4 + 2) / 2 = 3.0, recomputed from source, not incremented in place.
            Assert.Equal(3.0m, snapshot.AvgBandMeaningTransfer);
        }

        [Fact]
        public async Task A_failing_ai_narrative_still_leaves_the_recomputed_numeric_snapshot_in_place()
        {
            await using var context = _fixture.CreateContext();
            var (examType, user, question, dimension) = await SeedBaseDataAsync(context);

            await SeedGradedSubmissionAsync(
                context, question.Id, user.Id, dimension.Id,
                gradedAt: new DateTimeOffset(2026, 8, 19, 10, 0, 0, TimeSpan.Zero), band: 3, passBool: true);

            var fakeClient = new FakeAlwaysInvalidProgressTrendLlmClient();
            var handler = BuildHandler(context, LlmClientResolverSubstitute.Returning(fakeClient));

            var result = await handler.Handle(
                new GenerateProgressTrendSnapshotCommand(
                    user.Id, examType.Id, "medium", new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 23)),
                CancellationToken.None);

            // Unlike every other AI-orchestration handler in this codebase, an AI failure here
            // does not throw and does not roll back the numeric part — see the handler's own
            // class doc comment for why.
            Assert.False(result.Skipped);
            Assert.False(result.TrendNoteGenerated);
            Assert.NotNull(result.SnapshotId);
            // AiCallRetryExecutor's default MaxRetries=3 means 3 real attempts before giving up.
            Assert.Equal(3, fakeClient.CallCount);

            await using var readContext = _fixture.CreateContext();
            var snapshot = await readContext.ProgressSnapshots.SingleAsync(x => x.Id == result.SnapshotId);
            Assert.Equal(3.0m, snapshot.AvgBandMeaningTransfer);
            Assert.Equal(100.00m, snapshot.PassRate);
            Assert.Null(snapshot.TrendNote);
            Assert.False(snapshot.KeyTurningPoint);

            // PostgresCollection shares one Postgres container across every test in this class, so
            // scope by RelatedId (this test's own, freshly-generated UserId) rather than just
            // RequestType — otherwise this collides with the other progress_trend AiCallLog rows
            // the class's other tests create in the same database.
            var aiCallLog = await readContext.AiCallLogs.SingleAsync(
                x => x.RequestType == AiOperationType.progress_trend && x.RelatedId == user.Id);
            Assert.Equal(CallStatus.final_failure, aiCallLog.Status);
        }
    }
}
