using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ReviewLibrary.Commands.MarkPatternReviewed;
using DeepLearning.Application.Features.ReviewLibrary.Commands.MarkVocabReviewed;
using DeepLearning.Application.Features.ReviewLibrary.Queries.ListReviewPatterns;
using DeepLearning.Application.Features.ReviewLibrary.Queries.ListReviewVocab;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class ReviewLibraryControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public ReviewLibraryControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static User NewUser() => new()
        {
            Id = Guid.NewGuid(),
            Username = $"test_{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        [Fact]
        public async Task List_patterns_filters_by_domain_and_overlays_this_users_review_state()
        {
            var client = _factory.CreateClient();
            var domain = $"legal_{Guid.NewGuid():N}";
            var user = NewUser();

            var matchingPattern = new SentencePattern { Id = Guid.NewGuid(), PatternName = "非限定性定语从句", Domain = domain, CreatedAt = DateTimeOffset.UtcNow };
            var otherDomainPattern = new SentencePattern { Id = Guid.NewGuid(), PatternName = "倒装句", Domain = $"medical_{Guid.NewGuid():N}", CreatedAt = DateTimeOffset.UtcNow };

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await context.Users.AddAsync(user);
                await context.SentencePatterns.AddRangeAsync(matchingPattern, otherDomainPattern);
                await context.SaveChangesAsync();

                await context.UserPatternReview.AddAsync(new UserPatternReview
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    PatternId = matchingPattern.Id,
                    TimesEncountered = 3,
                    MasteryLevel = MasteryLevel.Familiar,
                    LastReviewedAt = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await context.SaveChangesAsync();
            }

            var response = await client.GetAsync($"{ApiRoutes.ReviewLibrary.Base}/patterns?userId={user.Id}&domain={domain}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var results = await response.Content.ReadFromJsonAsync<List<ReviewPatternResultItem>>();

            var item = Assert.Single(results!);
            Assert.Equal(matchingPattern.Id, item.Id);
            Assert.Equal(3, item.TimesEncountered);
            Assert.Equal(MasteryLevel.Familiar, item.MasteryLevel);
        }

        [Fact]
        public async Task List_patterns_defaults_a_never_reviewed_pattern_to_new_and_zero_encounters()
        {
            var client = _factory.CreateClient();
            // FrequencyTag is VARCHAR(20) — keep well under that instead of a full GUID suffix.
            var frequencyTag = $"tag_{Guid.NewGuid():N}"[..20];
            var pattern = new SentencePattern { Id = Guid.NewGuid(), PatternName = "被动语态", FrequencyTag = frequencyTag, CreatedAt = DateTimeOffset.UtcNow };

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await context.SentencePatterns.AddAsync(pattern);
                await context.SaveChangesAsync();
            }

            var response = await client.GetAsync($"{ApiRoutes.ReviewLibrary.Base}/patterns?userId={Guid.NewGuid()}&frequencyTag={frequencyTag}");
            var results = await response.Content.ReadFromJsonAsync<List<ReviewPatternResultItem>>();

            var item = Assert.Single(results!);
            Assert.Equal(0, item.TimesEncountered);
            Assert.Equal(MasteryLevel.New, item.MasteryLevel);
            Assert.Null(item.LastReviewedAt);
        }

        [Fact]
        public async Task Mark_pattern_reviewed_creates_a_review_row_then_updates_mastery_without_touching_times_encountered()
        {
            var client = _factory.CreateClient();
            var user = NewUser();
            SentencePattern pattern;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                pattern = new SentencePattern { Id = Guid.NewGuid(), PatternName = "虚拟语气", CreatedAt = DateTimeOffset.UtcNow };
                await context.Users.AddAsync(user);
                await context.SentencePatterns.AddAsync(pattern);
                await context.SaveChangesAsync();
            }

            var first = await client.PostAsJsonAsync(
                $"{ApiRoutes.ReviewLibrary.Base}/patterns/{pattern.Id}/review",
                new { UserId = user.Id, MasteryLevel = MasteryLevel.Familiar });
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            var firstResult = await first.Content.ReadFromJsonAsync<MarkPatternReviewedResult>();
            Assert.Equal(MasteryLevel.Familiar, firstResult!.MasteryLevel);
            Assert.Equal(1, firstResult.TimesEncountered);

            var second = await client.PostAsJsonAsync(
                $"{ApiRoutes.ReviewLibrary.Base}/patterns/{pattern.Id}/review",
                new { UserId = user.Id, MasteryLevel = MasteryLevel.Mastered });
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            var secondResult = await second.Content.ReadFromJsonAsync<MarkPatternReviewedResult>();
            Assert.Equal(MasteryLevel.Mastered, secondResult!.MasteryLevel);
            // Marking mastery is a distinct concept from ExtractKnowledgePointsOnGraded's
            // encounter counting (Step 6) — a manual review call must never bump it.
            Assert.Equal(1, secondResult.TimesEncountered);
            Assert.Equal(firstResult.Id, secondResult.Id);
        }

        /// <summary>
        /// Self-audit fix (2026-08-30): user_pattern_review/user_vocab_review have a real FK to
        /// users, but neither handler checked UserId existed before inserting — an unregistered
        /// UserId used to surface as a raw 500 (DbUpdateException) instead of a clean 404, unlike
        /// every other caller-supplied-id check in this codebase (e.g. GenerateQuestionCommand's
        /// CreatedBy). Fixed by validating via IUserRepository first, same convention.
        /// </summary>
        [Fact]
        public async Task Mark_pattern_reviewed_returns_404_for_an_unregistered_user_instead_of_a_raw_500()
        {
            var client = _factory.CreateClient();
            SentencePattern pattern;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                pattern = new SentencePattern { Id = Guid.NewGuid(), PatternName = "分词状语", CreatedAt = DateTimeOffset.UtcNow };
                await context.SentencePatterns.AddAsync(pattern);
                await context.SaveChangesAsync();
            }

            var response = await client.PostAsJsonAsync(
                $"{ApiRoutes.ReviewLibrary.Base}/patterns/{pattern.Id}/review",
                new { UserId = Guid.NewGuid(), MasteryLevel = MasteryLevel.Familiar });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Mark_pattern_reviewed_returns_404_for_an_unknown_pattern()
        {
            var client = _factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                $"{ApiRoutes.ReviewLibrary.Base}/patterns/{Guid.NewGuid()}/review",
                new { UserId = Guid.NewGuid(), MasteryLevel = MasteryLevel.New });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task List_vocab_filters_by_scenario_and_mark_vocab_reviewed_round_trips()
        {
            var client = _factory.CreateClient();
            var scenario = $"immigration_letter_{Guid.NewGuid():N}";
            var user = NewUser();
            var vocab = new VocabExpression { Id = Guid.NewGuid(), EnglishExpr = "in light of", Scenario = scenario, CreatedAt = DateTimeOffset.UtcNow };

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await context.Users.AddAsync(user);
                await context.VocabExpressions.AddAsync(vocab);
                await context.SaveChangesAsync();
            }

            var listResponse = await client.GetAsync($"{ApiRoutes.ReviewLibrary.Base}/vocab?userId={user.Id}&scenario={scenario}");
            var list = await listResponse.Content.ReadFromJsonAsync<List<ReviewVocabResultItem>>();
            Assert.Single(list!);

            var reviewResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.ReviewLibrary.Base}/vocab/{vocab.Id}/review",
                new { UserId = user.Id, MasteryLevel = MasteryLevel.Mastered });
            Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
            var reviewed = await reviewResponse.Content.ReadFromJsonAsync<MarkVocabReviewedResult>();
            Assert.Equal(MasteryLevel.Mastered, reviewed!.MasteryLevel);

            var listAfterResponse = await client.GetAsync($"{ApiRoutes.ReviewLibrary.Base}/vocab?userId={user.Id}&scenario={scenario}");
            var listAfter = await listAfterResponse.Content.ReadFromJsonAsync<List<ReviewVocabResultItem>>();
            Assert.Equal(MasteryLevel.Mastered, listAfter!.Single().MasteryLevel);
        }
    }
}
