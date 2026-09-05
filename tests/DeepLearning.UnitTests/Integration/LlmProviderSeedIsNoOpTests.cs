using DeepLearning.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace DeepLearning.UnitTests.Integration
{
    /// <summary>
    /// seed_llm_provider_defaults_for_fresh_db.sql exists for brand-new databases, but it sits at the tail
    /// of _manifest.txt, so `sql apply` will eventually run it against the SHARED database too. These tests
    /// answer the only question that matters there: can it change anything that is already set?
    ///
    /// <para>The shared database's provider config has been changed by hand and through the API since it
    /// was seeded — a different active provider, a different current model, possibly rows this script does
    /// not know about. Each test below puts the database into one of those states first, then runs the
    /// script and asserts the state is byte-identical afterwards.</para>
    /// </summary>
    [Collection(FreshDatabaseCollection.Name)]
    public class LlmProviderSeedIsNoOpTests
    {
        private const string ScriptName = "seed_llm_provider_defaults_for_fresh_db.sql";

        private readonly FreshDatabaseFixture _fixture;

        public LlmProviderSeedIsNoOpTests(FreshDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        private static string Script()
            => new EmbeddedSqlScriptSource().GetScripts().Single(s => s.Name == ScriptName).Content;

        private static async Task ExecuteAsync(string connectionString, string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>Everything the script could possibly touch, as one comparable value.</summary>
        private static Task<string> SnapshotAsync(string connectionString)
            => FreshDatabaseFixture.ScalarAsync<string>(connectionString, """
                SELECT md5(string_agg(x, '|' ORDER BY x)) FROM (
                    SELECT s::text AS x FROM llm_provider_settings s
                    UNION ALL SELECT m::text FROM llm_provider_models m
                ) rows
                """);

        /// <summary>A database already carrying the script's own effect, as a fresh install leaves it.</summary>
        private async Task<string> BootstrappedAsync()
        {
            var connectionString = await _fixture.CreateEmptyDatabaseAsync();
            await using var context = FreshDatabaseFixture.CreateContext(connectionString);
            await context.Database.MigrateAsync();

            var runner = new SqlScriptRunner(connectionString, new EmbeddedSqlScriptSource(), NullLogger<SqlScriptRunner>.Instance);
            var report = await runner.BootstrapAsync();
            Assert.True(report.Success, report.Error);
            return connectionString;
        }

        [Fact]
        public async Task Running_it_again_on_a_database_that_already_has_it_changes_nothing()
        {
            var db = await BootstrappedAsync();
            var before = await SnapshotAsync(db);

            await ExecuteAsync(db, Script());

            Assert.Equal(before, await SnapshotAsync(db));
        }

        [Fact]
        public async Task A_hand_switched_current_model_is_not_reverted_to_the_default()
        {
            // The realistic shared-database state: someone switched Claude from opus to sonnet via
            // POST /api/v1/llm-provider-settings/claude/models/{model}/select. The script's defaults must
            // not pull it back — and inserting its own is_current row would additionally violate
            // ux_llm_provider_models_single_current_per_provider and fail the whole `sql apply`.
            var db = await BootstrappedAsync();
            await ExecuteAsync(db, """
                UPDATE llm_provider_models SET is_current = false WHERE provider_key = 'claude';
                UPDATE llm_provider_models SET is_current = true
                WHERE provider_key = 'claude' AND model = 'claude-sonnet-5';
                """);
            var before = await SnapshotAsync(db);

            await ExecuteAsync(db, Script());

            Assert.Equal(before, await SnapshotAsync(db));
            Assert.Equal("claude-sonnet-5", await FreshDatabaseFixture.ScalarAsync<string>(
                db, "SELECT model FROM llm_provider_models WHERE provider_key = 'claude' AND is_current"));
        }

        [Fact]
        public async Task A_hand_switched_active_provider_is_not_reverted()
        {
            var db = await BootstrappedAsync();
            await ExecuteAsync(db, """
                UPDATE llm_provider_settings SET is_active = false WHERE is_active;
                UPDATE llm_provider_settings SET is_active = true WHERE provider_key = 'claude';
                """);
            var before = await SnapshotAsync(db);

            await ExecuteAsync(db, Script());

            Assert.Equal(before, await SnapshotAsync(db));
            Assert.Equal("claude", await FreshDatabaseFixture.ScalarAsync<string>(
                db, "SELECT provider_key FROM llm_provider_settings WHERE is_active"));
        }

        [Fact]
        public async Task A_model_the_script_does_not_know_about_is_left_in_place()
        {
            var db = await BootstrappedAsync();
            await ExecuteAsync(db, """
                UPDATE llm_provider_models SET is_current = false WHERE provider_key = 'deepseek';
                INSERT INTO llm_provider_models (provider_key, model, label, is_current)
                VALUES ('deepseek', 'deepseek-v4-pro', '手动加的型号', true);
                """);
            var before = await SnapshotAsync(db);

            await ExecuteAsync(db, Script());

            Assert.Equal(before, await SnapshotAsync(db));
        }

        [Fact]
        public async Task It_only_adds_rows_that_are_genuinely_absent()
        {
            // The one case where the script DOES write to an existing database: a provider row that was
            // never there. It inserts it as is_active = false / not current, so nothing in use changes.
            var db = await BootstrappedAsync();
            await ExecuteAsync(db, "DELETE FROM llm_provider_models WHERE provider_key = 'openai';");
            await ExecuteAsync(db, "DELETE FROM llm_provider_settings WHERE provider_key = 'openai';");

            var activeBefore = await FreshDatabaseFixture.ScalarAsync<string>(
                db, "SELECT provider_key FROM llm_provider_settings WHERE is_active");

            await ExecuteAsync(db, Script());

            Assert.Equal(1L, await FreshDatabaseFixture.ScalarAsync<long>(
                db, "SELECT count(*) FROM llm_provider_settings WHERE provider_key = 'openai' AND NOT is_active"));
            Assert.Equal(activeBefore, await FreshDatabaseFixture.ScalarAsync<string>(
                db, "SELECT provider_key FROM llm_provider_settings WHERE is_active"));
        }
    }
}
