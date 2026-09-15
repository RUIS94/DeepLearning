using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using DeepLearning.Api.Constants;
using DeepLearning.Api.Controllers;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class RateLimitingTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public RateLimitingTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private static readonly string RepoRoot = FindRepoRoot();

        private static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeepLearning.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException("Could not locate the repo root (no DeepLearning.slnx above the test assembly).");
        }

        /// <summary>
        /// Every <c>[EnableRateLimiting("name")]</c> on a controller action must name a policy
        /// actually registered in Program.cs's <c>AddRateLimiter</c> call, and vice versa — either
        /// side drifting (a typo, a renamed policy, a removed attribute) would only surface at
        /// request time in production (ASP.NET Core throws when an endpoint references an
        /// unregistered policy), never at build time. Program.cs's policies are built inside a
        /// lambda, so the registered side isn't reflectable — read as text instead, same technique
        /// Infrastructure/DevDatabaseSwitchConfigTests.cs already uses for this class of
        /// two-files-must-agree bug.
        /// </summary>
        [Fact]
        public void Every_EnableRateLimiting_attribute_names_a_policy_registered_in_Program_cs()
        {
            var programCs = File.ReadAllText(Path.Combine(RepoRoot, "src", "DeepLearning.Api", "Program.cs"));
            var registeredPolicies = Regex.Matches(programCs, @"options\.AddPolicy\(""([^""]+)""")
                .Select(m => m.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal);
            Assert.NotEmpty(registeredPolicies);

            var controllerTypes = new[]
            {
                typeof(SubmissionsController),
                typeof(QuestionsController),
                typeof(FollowUpThreadsController),
            };
            var attributedPolicies = controllerTypes
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Select(m => m.GetCustomAttribute<EnableRateLimitingAttribute>())
                .Where(a => a is not null)
                .Select(a => a!.PolicyName!)
                .ToHashSet(StringComparer.Ordinal);
            Assert.NotEmpty(attributedPolicies);

            // Symmetric on purpose: catches a typo'd attribute (references a name Program.cs never
            // registers) AND a policy Program.cs registers but no endpoint actually uses.
            Assert.Equal(registeredPolicies, attributedPolicies);
        }

        /// <summary>
        /// Proves the actual runtime behavior, not just the wiring: fixed-window-per-user limiting
        /// really does reject the caller past the configured limit, and really does partition by
        /// caller rather than globally — picked <c>ai-question-generate</c> because it needs the
        /// least setup (one ExamType FK, no submission/grading chain), not because it is special.
        /// </summary>
        [Fact]
        public async Task Generate_is_rate_limited_per_user_and_returns_429_past_the_limit()
        {
            // ai-question-generate's real limit, kept in sync with Program.cs by hand — if that
            // number changes, this test failing is the reminder to update it here too.
            const int PermitLimit = 30;

            var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
                services => services.AddScoped<ILlmClientResolver>(_ => LlmClientResolverSubstitute.Returning(new FakeLlmClient()))));

            var adminClient = factory.CreateClient();
            adminClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", ApiWebApplicationFactory.CreateJwt(await _factory.SeedUserAsync(UserRole.admin)));
            var examTypeResponse = await adminClient.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "Rate Limit Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            examTypeResponse.EnsureSuccessStatusCode();
            var examType = await examTypeResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();

            var userA = factory.CreateClient();
            userA.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", ApiWebApplicationFactory.CreateJwt(Guid.NewGuid()));

            object GenerateRequest() => new
            {
                ExamTypeId = examType!.Id,
                TaskType = TaskType.A,
                Difficulty = Difficulty.medium,
                CategoryId = (Guid?)null,
            };

            for (var i = 0; i < PermitLimit; i++)
            {
                var response = await userA.PostAsJsonAsync($"{ApiRoutes.Questions.Base}/generate", GenerateRequest());
                Assert.True(
                    response.StatusCode == HttpStatusCode.Created,
                    $"call {i + 1}/{PermitLimit} should still be under the limit, got {response.StatusCode}");
            }

            var overLimit = await userA.PostAsJsonAsync($"{ApiRoutes.Questions.Base}/generate", GenerateRequest());
            Assert.Equal((HttpStatusCode)429, overLimit.StatusCode);

            // A different, never-before-seen user has their own quota — proves the limiter
            // partitions by caller (the JWT "sub" claim), not globally across every caller.
            var userB = factory.CreateClient();
            userB.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", ApiWebApplicationFactory.CreateJwt(Guid.NewGuid()));
            var userBResponse = await userB.PostAsJsonAsync($"{ApiRoutes.Questions.Base}/generate", GenerateRequest());
            Assert.Equal(HttpStatusCode.Created, userBResponse.StatusCode);
        }
    }
}
