using DeepLearning.Application.Common;
using DeepLearning.Application.Features.Questions.Commands.GenerateDeepLearningContent;
using DeepLearning.Application.Features.ReviewLibrary.Commands.AnalyzeVocabSemanticDrift;
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

namespace DeepLearning.UnitTests.Integration
{
    /// <summary>
    /// vocab_expressions stays a per-question snapshot; the canonical cross-question record lives
    /// in vocab_glossary, seeded on first sight and (later, off-thread) enriched only when a
    /// recurrence introduces a new sense. Assembled by hand against a real Postgres container —
    /// same convention as the other Integration/*CommandHandlerTests.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class VocabGlossaryTests
    {
        private readonly PostgresContainerFixture _fixture;

        public VocabGlossaryTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        private sealed class NoOpPublisher : IPublisher
        {
            public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
                where TNotification : INotification
                => Task.CompletedTask;
        }

        private sealed class RecordingQueue : IVocabGlossaryQueue
        {
            public List<(Guid QuestionId, Guid ExamTypeId)> Enqueued { get; } = [];

            public Task EnqueueAsync(Guid questionId, Guid examTypeId, CancellationToken cancellationToken = default)
            {
                Enqueued.Add((questionId, examTypeId));
                return Task.CompletedTask;
            }
        }

        private sealed class StubDriftService(IReadOnlyDictionary<string, string> result) : IVocabSemanticDriftService
        {
            public Task<IReadOnlyDictionary<string, string>> AnalyzeAsync(
                Guid examTypeId, IReadOnlyList<VocabDriftItem> items, string sourceText, string taskType,
                CancellationToken cancellationToken = default)
                => Task.FromResult(result);
        }

        private static GenerateDeepLearningContentCommandHandler BuildGenerateHandler(
            AppDbContext context, ILlmClientResolver llm, IVocabGlossaryQueue queue)
            => new(
                new ExamTypeRepository(context),
                new QuestionRepository(context),
                new ReferenceTranslationRepository(context),
                new ReviewLibraryRepository(context),
                new AiCallLogRepository(context),
                new ExamConfigLoader(new ExamTypeRepository(context), new PromptTemplateRepository(context), new PromptRenderer()),
                llm,
                new AiCallRetryExecutor(TimeSpan.FromMilliseconds(1)),
                queue,
                new UnitOfWork(context, new NoOpPublisher()));

        private async Task<(ExamType Exam, Question Q1, Question Q2)> SeedAsync()
        {
            await using var context = _fixture.CreateContext();
            var exam = new ExamType
            {
                Id = Guid.NewGuid(),
                Code = $"t_{Guid.NewGuid():N}",
                Name = "T",
                SubjectCategory = SubjectCategory.translation,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var owner = new User
            {
                Id = Guid.NewGuid(),
                Username = $"test_{Guid.NewGuid():N}",
                Email = $"{Guid.NewGuid():N}@test.local",
                Role = UserRole.user,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var ownerId = owner.Id;
            Question NewQuestion(string src) => new()
            {
                Id = Guid.NewGuid(),
                TaskType = TaskType.A,
                Difficulty = Difficulty.medium,
                Title = "Q",
                SourceText = src,
                Origin = QuestionOrigin.user_uploaded,
                SourceType = SourceType.user_generated,
                Visibility = Visibility.Private,
                CreatedBy = ownerId,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var q1 = NewQuestion("First passage, in light of recent events.");
            var q2 = NewQuestion("Second passage, in light of the review.");
            await context.Users.AddAsync(owner);
            await context.ExamTypes.AddAsync(exam);
            await context.Questions.AddRangeAsync(q1, q2);
            await context.SaveChangesAsync();
            return (exam, q1, q2);
        }

        [Fact]
        public async Task Recurring_expression_gets_one_glossary_row_across_two_questions_and_enqueues_drift_only_on_recurrence()
        {
            var (exam, q1, q2) = await SeedAsync();
            var llm = LlmClientResolverSubstitute.Returning(new FakeDeepLearningLlmClient());
            var queue = new RecordingQueue();

            await using (var c = _fixture.CreateContext())
            {
                await BuildGenerateHandler(c, llm, queue).Handle(
                    new GenerateDeepLearningContentCommand(q1.Id, exam.Id, q1.CreatedBy!.Value), CancellationToken.None);
            }
            await using (var c = _fixture.CreateContext())
            {
                await BuildGenerateHandler(c, llm, queue).Handle(
                    new GenerateDeepLearningContentCommand(q2.Id, exam.Id, q2.CreatedBy!.Value), CancellationToken.None);
            }

            await using var read = _fixture.CreateContext();
            var key = FakeDeepLearningLlmClient.VocabExpr.ToLowerInvariant();

            var snapshots = await read.VocabExpressions.Where(v => v.CanonicalKey == key).ToListAsync();
            Assert.Equal(2, snapshots.Count);
            Assert.Equal(new[] { q1.Id, q2.Id }.OrderBy(x => x), snapshots.Select(s => s.QuestionId!.Value).OrderBy(x => x));

            var glossary = await read.VocabGlossary.SingleAsync(g => g.CanonicalKey == key);
            Assert.Equal(2, glossary.OccurrenceCount);
            Assert.Equal(1, glossary.SenseCount);
            Assert.Equal(q1.Id, glossary.FirstSeenQuestionId);

            // Only the second (recurring) generation enqueues drift analysis.
            var enqueued = Assert.Single(queue.Enqueued);
            Assert.Equal((q2.Id, exam.Id), enqueued);
        }

        [Fact]
        public async Task Drift_handler_updates_glossary_only_for_changed_entries_and_never_touches_snapshots()
        {
            var (exam, q1, q2) = await SeedAsync();
            var llm = LlmClientResolverSubstitute.Returning(new FakeDeepLearningLlmClient());
            var queue = new RecordingQueue();
            foreach (var q in new[] { q1, q2 })
            {
                await using var c = _fixture.CreateContext();
                await BuildGenerateHandler(c, llm, queue).Handle(
                    new GenerateDeepLearningContentCommand(q.Id, exam.Id, q.CreatedBy!.Value), CancellationToken.None);
            }

            var key = FakeDeepLearningLlmClient.VocabExpr.ToLowerInvariant();

            await using (var c = _fixture.CreateContext())
            {
                var handler = new AnalyzeVocabSemanticDriftForQuestionCommandHandler(
                    new QuestionRepository(c),
                    new ReviewLibraryRepository(c),
                    new StubDriftService(new Dictionary<string, string> { [key] = "① 鉴于。② 就……而言(本篇新义)。" }),
                    new UnitOfWork(c, new NoOpPublisher()));
                await handler.Handle(new AnalyzeVocabSemanticDriftForQuestionCommand(q2.Id, exam.Id), CancellationToken.None);
            }

            await using var read = _fixture.CreateContext();
            var glossary = await read.VocabGlossary.SingleAsync(g => g.CanonicalKey == key);
            Assert.Contains("本篇新义", glossary.AccumulatedSemantics);
            Assert.Equal(2, glossary.SenseCount);
            // snapshots untouched
            var snapshots = await read.VocabExpressions.Where(v => v.CanonicalKey == key).ToListAsync();
            Assert.All(snapshots, s => Assert.Null(s.ContextNote));

            // A no-op drift result leaves the row alone.
            await using (var c = _fixture.CreateContext())
            {
                var handler = new AnalyzeVocabSemanticDriftForQuestionCommandHandler(
                    new QuestionRepository(c),
                    new ReviewLibraryRepository(c),
                    new StubDriftService(new Dictionary<string, string>()),
                    new UnitOfWork(c, new NoOpPublisher()));
                await handler.Handle(new AnalyzeVocabSemanticDriftForQuestionCommand(q1.Id, exam.Id), CancellationToken.None);
            }
            await using var read2 = _fixture.CreateContext();
            var after = await read2.VocabGlossary.SingleAsync(g => g.CanonicalKey == key);
            Assert.Equal(2, after.SenseCount);
        }

        [Fact]
        public async Task Glossary_canonical_key_is_unique()
        {
            await using var context = _fixture.CreateContext();
            var key = $"dup_{Guid.NewGuid():N}";
            VocabGlossaryEntry Make() => new()
            {
                Id = Guid.NewGuid(),
                CanonicalKey = key,
                EnglishExpr = "x",
                AccumulatedSemantics = "s",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            await context.VocabGlossary.AddAsync(Make());
            await context.SaveChangesAsync();

            await using var context2 = _fixture.CreateContext();
            await context2.VocabGlossary.AddAsync(Make());
            await Assert.ThrowsAsync<DbUpdateException>(() => context2.SaveChangesAsync());
        }

        [Fact]
        public async Task ListRecurringVocabForQuestion_returns_only_keys_seen_on_more_than_one_question()
        {
            var (_, q1, q2) = await SeedAsync();

            await using var context = _fixture.CreateContext();
            var qa = q1.Id;
            var qb = q2.Id;
            VocabExpression V(Guid qid, string key) => new()
            {
                Id = Guid.NewGuid(),
                QuestionId = qid,
                EnglishExpr = key,
                CanonicalKey = key,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            var shared = $"shared_{Guid.NewGuid():N}";
            var onlyA = $"only_a_{Guid.NewGuid():N}";
            await context.VocabExpressions.AddRangeAsync(V(qa, shared), V(qb, shared), V(qa, onlyA));
            await context.SaveChangesAsync();

            var repo = new ReviewLibraryRepository(context);
            var recurring = await repo.ListRecurringVocabForQuestionAsync(qa, CancellationToken.None);

            Assert.Equal(new[] { shared }, recurring.Select(r => r.CanonicalKey));
        }
    }
}
