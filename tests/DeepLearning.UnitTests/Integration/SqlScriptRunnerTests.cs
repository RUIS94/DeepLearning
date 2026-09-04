using DeepLearning.Infrastructure.Persistence.Sql;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace DeepLearning.UnitTests.Integration
{
    /// <summary>
    /// SqlScriptRunner against a real throwaway Postgres. Each test uses its own fake script
    /// source with Guid-suffixed script + table names, so the shared <c>_sql_scripts</c> table
    /// (keyed by script name) never crosses tests.
    ///
    /// The load-bearing guarantee under test: <c>apply</c> can never re-run a historical script
    /// against a database that was hand-edited outside these files — it refuses without a
    /// baseline and only ever runs the manifest tail after everything already recorded.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class SqlScriptRunnerTests
    {
        private readonly PostgresContainerFixture _fixture;

        public SqlScriptRunnerTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        private sealed class FakeSource(params SqlScript[] scripts) : ISqlScriptSource
        {
            private readonly List<SqlScript> _scripts = [.. scripts];

            public void Add(SqlScript script) => _scripts.Add(script);

            public IReadOnlyList<SqlScript> GetScripts() => _scripts;
        }

        private SqlScriptRunner Runner(ISqlScriptSource source)
            => new(_fixture.ConnectionString, source, NullLogger<SqlScriptRunner>.Instance);

        private static SqlScript CreatesTable(string name, string table)
            => new(name, $"BEGIN;\nCREATE TABLE {table} (id int PRIMARY KEY);\nINSERT INTO {table} (id) VALUES (1);\nCOMMIT;");

        private async Task<bool> TableExistsAsync(string table)
        {
            await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT to_regclass(@t) IS NOT NULL", conn);
            cmd.Parameters.AddWithValue("t", table);
            return (bool)(await cmd.ExecuteScalarAsync())!;
        }

        private async Task<string?> NoteForAsync(string scriptName)
        {
            await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"SELECT note FROM {SqlScriptRunner.TrackingTable} WHERE name = @n", conn);
            cmd.Parameters.AddWithValue("n", scriptName);
            return (string?)await cmd.ExecuteScalarAsync();
        }

        [Fact]
        public async Task Apply_refuses_when_there_is_no_baseline()
        {
            var tag = Guid.NewGuid().ToString("N");
            var source = new FakeSource(CreatesTable($"a_{tag}.sql", $"t_a_{tag}"));

            var report = await Runner(source).ApplyAsync(baselineOnly: false);

            Assert.False(report.Success);
            Assert.Contains("no baseline", report.Error);
            Assert.Empty(report.Ran);
            Assert.False(await TableExistsAsync($"t_a_{tag}"));
        }

        [Fact]
        public async Task Baseline_records_scripts_without_executing_them()
        {
            var tag = Guid.NewGuid().ToString("N");
            var source = new FakeSource(CreatesTable($"a_{tag}.sql", $"t_a_{tag}"));

            var report = await Runner(source).ApplyAsync(baselineOnly: true);

            Assert.True(report.Success);
            Assert.Empty(report.Ran);
            Assert.Equal([$"a_{tag}.sql"], report.Recorded);
            Assert.Equal("baseline", await NoteForAsync($"a_{tag}.sql"));
            Assert.False(await TableExistsAsync($"t_a_{tag}"));
        }

        [Fact]
        public async Task Apply_after_baseline_runs_only_scripts_appended_at_the_tail()
        {
            var tag = Guid.NewGuid().ToString("N");
            var source = new FakeSource(
                CreatesTable($"a_{tag}.sql", $"t_a_{tag}"),
                CreatesTable($"b_{tag}.sql", $"t_b_{tag}"));

            await Runner(source).ApplyAsync(baselineOnly: true);

            source.Add(CreatesTable($"c_{tag}.sql", $"t_c_{tag}"));
            source.Add(CreatesTable($"d_{tag}.sql", $"t_d_{tag}"));
            var report = await Runner(source).ApplyAsync(baselineOnly: false);

            Assert.True(report.Success);
            Assert.Equal([$"c_{tag}.sql", $"d_{tag}.sql"], report.Ran);
            Assert.True(await TableExistsAsync($"t_c_{tag}"));
            Assert.True(await TableExistsAsync($"t_d_{tag}"));
            Assert.False(await TableExistsAsync($"t_a_{tag}")); // baselined, never executed
            Assert.Equal("applied", await NoteForAsync($"c_{tag}.sql"));

            // Re-run: nothing left pending.
            var again = await Runner(source).ApplyAsync(baselineOnly: false);
            Assert.True(again.Success);
            Assert.Empty(again.Ran);
        }

        [Fact]
        public async Task Apply_refuses_to_back_fill_a_gap_before_an_already_recorded_script()
        {
            var tag = Guid.NewGuid().ToString("N");
            var early = CreatesTable($"1_early_{tag}.sql", $"t_early_{tag}");
            var late = CreatesTable($"3_late_{tag}.sql", $"t_late_{tag}");

            // Baseline only the LATE script (simulates the early one having been added to the
            // manifest after the DB was already ahead of it).
            await Runner(new FakeSource(late)).ApplyAsync(baselineOnly: true);

            var full = new FakeSource(early, CreatesTable($"2_mid_{tag}.sql", $"t_mid_{tag}"), late);
            var report = await Runner(full).ApplyAsync(baselineOnly: false);

            Assert.False(report.Success);
            Assert.Contains("never back-fills", report.Error);
            Assert.Contains($"1_early_{tag}.sql", report.Error);
            Assert.Empty(report.Ran);
            Assert.False(await TableExistsAsync($"t_early_{tag}"));
            Assert.False(await TableExistsAsync($"t_mid_{tag}"));
        }

        [Fact]
        public async Task A_failing_script_stops_the_run_and_is_not_recorded()
        {
            var tag = Guid.NewGuid().ToString("N");
            var baseScript = CreatesTable($"0_base_{tag}.sql", $"t_base_{tag}");
            await Runner(new FakeSource(baseScript)).ApplyAsync(baselineOnly: true);

            var source = new FakeSource(
                baseScript,
                CreatesTable($"1_ok_{tag}.sql", $"t_ok_{tag}"),
                new SqlScript($"2_bad_{tag}.sql", "BEGIN;\nCREATE TABLE nonsense (id int) THIS IS NOT SQL;\nCOMMIT;"),
                CreatesTable($"3_never_{tag}.sql", $"t_never_{tag}"));

            var report = await Runner(source).ApplyAsync(baselineOnly: false);

            Assert.False(report.Success);
            Assert.Equal($"2_bad_{tag}.sql", report.FailedScript);
            Assert.Equal([$"1_ok_{tag}.sql"], report.Ran);
            Assert.Null(await NoteForAsync($"2_bad_{tag}.sql"));
            Assert.False(await TableExistsAsync($"t_never_{tag}"));
        }

        [Fact]
        public async Task Status_splits_applied_from_pending()
        {
            var tag = Guid.NewGuid().ToString("N");
            var source = new FakeSource(
                CreatesTable($"done_{tag}.sql", $"t_done_{tag}"),
                CreatesTable($"todo_{tag}.sql", $"t_todo_{tag}"));

            await Runner(source).ApplyAsync(baselineOnly: true);
            source.Add(CreatesTable($"todo2_{tag}.sql", $"t_todo2_{tag}"));

            var status = await Runner(source).GetStatusAsync();

            Assert.Contains($"done_{tag}.sql", status.Applied);
            Assert.Contains($"todo_{tag}.sql", status.Applied);
            Assert.Contains($"todo2_{tag}.sql", status.Pending);
            Assert.DoesNotContain($"todo2_{tag}.sql", status.Applied);
        }

        [Fact]
        public void EmbeddedSqlScriptSource_manifest_and_embedded_scripts_are_in_sync()
        {
            // Throws if _manifest.txt and the embedded *.sql set disagree either way.
            var scripts = new EmbeddedSqlScriptSource().GetScripts();

            Assert.Equal(40, scripts.Count);
            Assert.Equal("schema.sql", scripts[0].Name);
            Assert.Equal("rebuild_grading_prompt_v5_field_confusion.sql", scripts[^1].Name);
            Assert.All(scripts, s => Assert.False(string.IsNullOrWhiteSpace(s.Content)));
        }
    }
}
