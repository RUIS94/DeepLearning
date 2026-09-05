using System.Net;
using System.Net.Http.Json;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Ai;
using DeepLearning.Infrastructure.Ai.Options;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>
    /// Closes the test-coverage gap flagged in AGENTS.md's "AI integration" section (found by
    /// audit on 2026-08-29): no test previously exercised the full real chain — real Postgres
    /// rows -> LlmClientResolver querying them -> keyed-DI resolution -> an actual HTTP call.
    /// LlmClientResolverTests (Infrastructure/Ai) covers the resolver's merge/fallback logic
    /// against fake repositories, never a real Postgres. GenerateQuestionControllerTests swaps
    /// ILlmClientResolver itself for a fake. ClaudeLlmClientLiveTests/OpenAiCompatibleLlmClientLiveTests
    /// resolve the keyed ILlmClient directly, bypassing the resolver and the database entirely.
    ///
    /// This test runs the real resolver against real Postgres rows (the same Testcontainers
    /// instance ApiWebApplicationFactory already migrates) and the real ClaudeLlmClient /
    /// OpenAiCompatibleLlmClient adapter classes (real request building, real response parsing)
    /// end to end — but swaps just the innermost HttpMessageHandler for a canned in-memory one
    /// via WithWebHostBuilder, so it costs zero tokens/dollars and needs no network or API keys.
    /// Deliberately NOT tagged LlmIntegration — safe to run on every `dotnet test`.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class LlmClientResolverFullChainTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public LlmClientResolverFullChainTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private class CannedHandler : HttpMessageHandler
        {
            public string? CapturedBody { get; private set; }
            public required HttpResponseMessage ResponseToReturn { get; init; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                return ResponseToReturn;
            }
        }

        private static HttpResponseMessage ClaudeCannedResponse(string text) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                content = new[] { new { type = "text", text } },
                model = "claude-canned-model",
                usage = new { input_tokens = 3, output_tokens = 5 },
            }),
        };

        private static HttpResponseMessage OpenAiCompatibleCannedResponse(string text) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                choices = new[] { new { message = new { content = text } } },
                model = "deepseek-canned-model",
                usage = new { prompt_tokens = 7, completion_tokens = 11 },
            }),
        };

        /// <summary>
        /// Same defensive pattern as LlmProviderSettingsControllerTests.SeedTwoProvidersAsync:
        /// this Postgres is shared across the whole ApiCollection and rows from earlier tests
        /// are never cleaned up, so clear any pre-existing active/current flag before seeding
        /// this test's own row rather than assuming a clean table.
        /// </summary>
        private static async Task SeedActiveProviderAsync(AppDbContext context, string providerKey, string model)
        {
            await context.LlmProviderSettings.Where(x => x.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
            await context.LlmProviderModels.Where(x => x.ProviderKey == providerKey && x.IsCurrent)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsCurrent, false));

            var existingSettings = await context.LlmProviderSettings.SingleOrDefaultAsync(x => x.ProviderKey == providerKey);
            if (existingSettings is null)
            {
                context.LlmProviderSettings.Add(new LlmProviderSettings { Id = Guid.NewGuid(), ProviderKey = providerKey, IsActive = true });
            }
            else
            {
                existingSettings.IsActive = true;
            }

            var existingModel = await context.LlmProviderModels.SingleOrDefaultAsync(x => x.ProviderKey == providerKey && x.Model == model);
            if (existingModel is null)
            {
                context.LlmProviderModels.Add(new LlmProviderModel { Id = Guid.NewGuid(), ProviderKey = providerKey, Model = model, IsCurrent = true });
            }
            else
            {
                existingModel.IsCurrent = true;
            }

            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task Real_postgres_row_drives_the_real_ClaudeLlmClient_through_the_resolver_with_no_network_call()
        {
            var claudeHandler = new CannedHandler { ResponseToReturn = ClaudeCannedResponse("PONG") };

            await using var customized = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                // Registered after AddInfrastructure's own "claude" registration — DI resolves the
                // last registration for a given (service, key) pair, so this one wins. Still the
                // real ClaudeLlmClient class — only its HttpClient's transport is faked.
                services.AddKeyedTransient<ILlmClient>("claude", (_, _) => new ClaudeLlmClient(
                    new HttpClient(claudeHandler) { BaseAddress = new Uri("https://example.test") },
                    Options.Create(new ClaudeApiOptions { Model = "fallback-should-not-be-used" })));
            }));

            using (var seedScope = customized.Services.CreateScope())
            {
                await SeedActiveProviderAsync(
                    seedScope.ServiceProvider.GetRequiredService<AppDbContext>(),
                    "claude",
                    "claude-full-chain-test-model");
            }

            using var scope = customized.Services.CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<ILlmClientResolver>();
            var resolved = await resolver.GetActiveClientAsync(AiOperationType.grading);

            var result = await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "ping", MaxTokens: 16));

            Assert.Equal("PONG", result.Text);
            Assert.Contains("\"claude-full-chain-test-model\"", claudeHandler.CapturedBody);
        }

        [Fact]
        public async Task Real_postgres_row_drives_the_real_OpenAiCompatibleLlmClient_through_the_resolver_with_no_network_call()
        {
            var deepseekHandler = new CannedHandler { ResponseToReturn = OpenAiCompatibleCannedResponse("PONG") };

            await using var customized = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.AddKeyedTransient<ILlmClient>("deepseek", (_, _) => new OpenAiCompatibleLlmClient(
                    new HttpClient(deepseekHandler),
                    new OpenAiCompatibleOptions { BaseUrl = "https://example.test/v1/chat/completions", Model = "fallback-should-not-be-used" },
                    "DeepSeek"));
            }));

            using (var seedScope = customized.Services.CreateScope())
            {
                await SeedActiveProviderAsync(
                    seedScope.ServiceProvider.GetRequiredService<AppDbContext>(),
                    "deepseek",
                    "deepseek-full-chain-test-model");
            }

            using var scope = customized.Services.CreateScope();
            var resolver = scope.ServiceProvider.GetRequiredService<ILlmClientResolver>();
            var resolved = await resolver.GetActiveClientAsync(AiOperationType.grading);

            var result = await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "ping", MaxTokens: 16));

            Assert.Equal("PONG", result.Text);
            Assert.Contains("\"deepseek-full-chain-test-model\"", deepseekHandler.CapturedBody);
        }
    }
}
