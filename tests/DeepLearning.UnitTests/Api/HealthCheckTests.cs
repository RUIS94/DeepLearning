using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class HealthCheckTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public HealthCheckTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Health_endpoint_returns_ok()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        /// <summary>
        /// "Which database is this backend actually attached to?" has to be answerable from outside
        /// the process. A startup log line is not enough — the whole point of the Supabase/LocalDocker
        /// switch is being able to confirm, mid-session, that the thing you are about to write test
        /// data into is the throwaway one.
        /// </summary>
        [Fact]
        public async Task Health_db_reports_the_database_this_process_is_attached_to()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/health/db");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            // The test host runs against a Testcontainers Postgres on this machine with no
            // DB_PROFILE set, so the endpoint must report an INFERRED local target — declared:false
            // is what keeps the report from overstating what it knows.
            Assert.Equal("LocalDocker", body.GetProperty("profile").GetString());
            Assert.False(body.GetProperty("declared").GetBoolean());
            Assert.True(body.GetProperty("isLocal").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("host").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("database").GetString()));
        }

        [Fact]
        public async Task Health_db_never_exposes_credentials()
        {
            var client = _factory.CreateClient();

            // Anonymous endpoint — it must carry no more than host/port/database.
            var body = await (await client.GetAsync("/health/db")).Content.ReadAsStringAsync();

            Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("username", body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
