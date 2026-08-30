using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateAssessmentDimension;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.ExamConfig.Queries.GetAssessmentDimensionsByExamType;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class AssessmentDimensionsControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public AssessmentDimensionsControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<Guid> CreateExamTypeAsync(HttpClient client)
        {
            var response = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CreateExamTypeResult>();
            return result!.Id;
        }

        [Fact]
        public async Task Create_then_list_round_trips_over_http()
        {
            var client = _factory.CreateClient();
            var examTypeId = await CreateExamTypeAsync(client);
            var baseUrl = ApiRoutes.AssessmentDimensions.Base.Replace("{examTypeId:guid}", examTypeId.ToString());

            var createResponse = await client.PostAsJsonAsync(baseUrl, new
            {
                DimensionKey = "meaning_transfer",
                DimensionName = "Meaning transfer",
                ScaleType = ScaleType.band_1_5,
                PassThreshold = "Band 2 or above",
                ApplicableTaskType = TaskType.A,
                LevelDescriptions = "{\"1\":\"...\"}",
                RubricVersion = "2024-02",
                EffectiveFrom = DateTimeOffset.UtcNow,
                EffectiveTo = (DateTimeOffset?)null,
                SourceReference = (string?)null,
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var listResponse = await client.GetAsync(baseUrl);
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            var items = await listResponse.Content.ReadFromJsonAsync<List<AssessmentDimensionResultItem>>();
            Assert.Single(items!);
        }

        /// <summary>
        /// Self-audit fix (2026-08-31, design doc §10.1): creating a new rubric_version for a
        /// dimension_key that already has an open-ended version now closes the old one out
        /// (EffectiveTo = the new version's EffectiveFrom) instead of leaving both simultaneously
        /// "current" — proven both via the list endpoint (only the new version shows up) and by
        /// reading the old row directly (its EffectiveTo really did get set, to the right value).
        /// </summary>
        [Fact]
        public async Task Creating_a_new_rubric_version_closes_out_the_previously_open_ended_version()
        {
            var client = _factory.CreateClient();
            var examTypeId = await CreateExamTypeAsync(client);
            var baseUrl = ApiRoutes.AssessmentDimensions.Base.Replace("{examTypeId:guid}", examTypeId.ToString());
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var v1EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-10);
            var v2EffectiveFrom = DateTimeOffset.UtcNow;

            var v1Response = await client.PostAsJsonAsync(baseUrl, new
            {
                DimensionKey = dimensionKey,
                DimensionName = "Meaning transfer",
                ScaleType = ScaleType.band_1_5,
                PassThreshold = "Band 2 or above",
                ApplicableTaskType = (TaskType?)null,
                LevelDescriptions = "{\"1\":\"...\"}",
                RubricVersion = "2024-01",
                EffectiveFrom = v1EffectiveFrom,
                EffectiveTo = (DateTimeOffset?)null,
                SourceReference = (string?)null,
            });
            Assert.Equal(HttpStatusCode.Created, v1Response.StatusCode);
            var v1 = await v1Response.Content.ReadFromJsonAsync<CreateAssessmentDimensionResult>();

            var v2Response = await client.PostAsJsonAsync(baseUrl, new
            {
                DimensionKey = dimensionKey,
                DimensionName = "Meaning transfer",
                ScaleType = ScaleType.band_1_5,
                PassThreshold = "Band 2 or above",
                ApplicableTaskType = (TaskType?)null,
                LevelDescriptions = "{\"1\":\"...\"}",
                RubricVersion = "2024-02",
                EffectiveFrom = v2EffectiveFrom,
                EffectiveTo = (DateTimeOffset?)null,
                SourceReference = (string?)null,
            });
            Assert.Equal(HttpStatusCode.Created, v2Response.StatusCode);

            var listResponse = await client.GetAsync(baseUrl);
            var items = await listResponse.Content.ReadFromJsonAsync<List<AssessmentDimensionResultItem>>();
            var match = Assert.Single(items!, x => x.DimensionKey == dimensionKey);
            Assert.Equal("2024-02", match.RubricVersion);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var oldRow = await context.AssessmentDimensions.SingleAsync(x => x.Id == v1!.Id);
            Assert.NotNull(oldRow.EffectiveTo);
            // Not exact equality — Postgres's timestamptz has microsecond precision, .NET
            // DateTimeOffset has tick (100ns) precision, so a round trip through the DB can lose
            // a sliver of sub-microsecond precision. Within a second is plenty to prove "closed
            // out at the new version's start," not a flaky exact-tick comparison.
            Assert.True(Math.Abs((oldRow.EffectiveTo!.Value - v2EffectiveFrom).TotalSeconds) < 1);
        }

        /// <summary>
        /// Guards against silently corrupting the audit chain: a new version's EffectiveFrom must
        /// be strictly after whatever it's about to supersede, or the "closed" row would end up
        /// with EffectiveTo before its own EffectiveFrom.
        /// </summary>
        [Fact]
        public async Task Creating_a_new_version_that_would_start_before_the_current_one_returns_400()
        {
            var client = _factory.CreateClient();
            var examTypeId = await CreateExamTypeAsync(client);
            var baseUrl = ApiRoutes.AssessmentDimensions.Base.Replace("{examTypeId:guid}", examTypeId.ToString());
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var v1EffectiveFrom = DateTimeOffset.UtcNow;

            var v1Response = await client.PostAsJsonAsync(baseUrl, new
            {
                DimensionKey = dimensionKey,
                DimensionName = "Meaning transfer",
                ScaleType = ScaleType.band_1_5,
                PassThreshold = "Band 2 or above",
                ApplicableTaskType = (TaskType?)null,
                LevelDescriptions = "{\"1\":\"...\"}",
                RubricVersion = "2024-01",
                EffectiveFrom = v1EffectiveFrom,
                EffectiveTo = (DateTimeOffset?)null,
                SourceReference = (string?)null,
            });
            Assert.Equal(HttpStatusCode.Created, v1Response.StatusCode);

            var v2Response = await client.PostAsJsonAsync(baseUrl, new
            {
                DimensionKey = dimensionKey,
                DimensionName = "Meaning transfer",
                ScaleType = ScaleType.band_1_5,
                PassThreshold = "Band 2 or above",
                ApplicableTaskType = (TaskType?)null,
                LevelDescriptions = "{\"1\":\"...\"}",
                RubricVersion = "2024-02",
                EffectiveFrom = v1EffectiveFrom.AddDays(-1), // earlier than v1 — out of order
                EffectiveTo = (DateTimeOffset?)null,
                SourceReference = (string?)null,
            });

            Assert.Equal(HttpStatusCode.BadRequest, v2Response.StatusCode);
        }

        [Fact]
        public async Task Create_returns_404_for_unknown_exam_type()
        {
            var client = _factory.CreateClient();
            var baseUrl = ApiRoutes.AssessmentDimensions.Base.Replace("{examTypeId:guid}", Guid.NewGuid().ToString());

            var response = await client.PostAsJsonAsync(baseUrl, new
            {
                DimensionKey = "meaning_transfer",
                DimensionName = "Meaning transfer",
                ScaleType = ScaleType.band_1_5,
                LevelDescriptions = "{\"1\":\"...\"}",
                RubricVersion = "2024-02",
                EffectiveFrom = DateTimeOffset.UtcNow,
            });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
