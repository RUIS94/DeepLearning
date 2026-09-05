using DeepLearning.Api;
using DeepLearning.Infrastructure;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.Infrastructure.Persistence.Sql;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepLearning.UnitTests.Integration
{
    /// <summary>
    /// `sql bootstrap` against a genuinely empty database — the path a throwaway docker container
    /// takes every time it is re-created.
    ///
    /// Nothing tested a fresh install before this. That gap is what made a fresh local database
    /// unusable in practice: `sql apply` refuses without a baseline, `sql baseline` records the seed
    /// scripts as done without running them, and the manifest is not a valid install order anyway
    /// (add_llm_provider_models.sql and upgrade_llm_provider_models_schema.sql are alternatives to
    /// each other, and both collide with the EF migration that already creates that table). So the
    /// load-bearing assertion here is not "bootstrap returns 0" — it is that the reference tables a
    /// running app reads on every request come out populated, matching what the shared database has.
    /// </summary>
    [Collection(FreshDatabaseCollection.Name)]
    public class SqlBootstrapTests
    {
        private readonly FreshDatabaseFixture _fixture;

        public SqlBootstrapTests(FreshDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        /// <summary>
        /// A service provider wired exactly as Program.cs wires it (AddInfrastructure), so the test
        /// exercises the real DI graph — including the DB_PROFILE cross-check — rather than a
        /// hand-assembled runner that could drift from it.
        /// </summary>
        private static ServiceProvider BuildServices(string connectionString, string? dbProfile)
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["DB_PROFILE"] = dbProfile,
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            return new ServiceCollection()
                .AddLogging()
                .AddInfrastructure(configuration)
                .BuildServiceProvider();
        }

        private static Task<long> CountAsync(string connectionString, string table)
            => FreshDatabaseFixture.ScalarAsync<long>(connectionString, $"SELECT count(*) FROM {table}");

        [Fact]
        public async Task Bootstrap_brings_an_empty_database_up_with_the_shared_reference_data()
        {
            var connectionString = await _fixture.CreateEmptyDatabaseAsync();
            await using var services = BuildServices(connectionString, "LocalDocker");

            var exitCode = await SqlCli.RunAsync("bootstrap", services);
            Assert.Equal(0, exitCode);

            // --- the tables an app instance reads on ordinary requests ------------------------
            Assert.Equal("naati_ct_en_zh", await FreshDatabaseFixture.ScalarAsync<string>(
                connectionString, "SELECT code FROM exam_types WHERE is_active ORDER BY code LIMIT 1"));

            Assert.True(await CountAsync(connectionString, "assessment_dimensions") > 0);
            Assert.True(await CountAsync(connectionString, "error_taxonomies") > 0);
            Assert.True(await CountAsync(connectionString, "generation_policy") > 0);
            Assert.True(await CountAsync(connectionString, "llm_provider_settings") > 0);
            Assert.True(await CountAsync(connectionString, "llm_provider_models") > 0);
            Assert.True(await CountAsync(connectionString, "prompt_templates") > 0);
            Assert.True(await CountAsync(connectionString, "weak_point_catalog") > 0);

            // ExamConfigLoader concatenates every ACTIVE row of a template type, so a second active
            // grading row would silently send two rubrics in one call. The last script in the
            // manifest exists to leave exactly one.
            Assert.Equal(1L, await FreshDatabaseFixture.ScalarAsync<long>(
                connectionString, "SELECT count(*) FROM prompt_templates WHERE template_type = 'grading' AND is_active"));

            // Every dimension must know which task type it belongs to, or the grading engine cannot
            // pick its dimension set — the column is added by a seed script, not by EF.
            Assert.Equal(0L, await FreshDatabaseFixture.ScalarAsync<long>(
                connectionString, "SELECT count(*) FROM assessment_dimensions WHERE applicable_task_type IS NULL"));

            // At most one current model per provider (the partial unique index only forbids a
            // second one; this asserts there is actually one).
            Assert.True(await FreshDatabaseFixture.ScalarAsync<long>(
                connectionString, "SELECT count(*) FROM llm_provider_models WHERE is_current") > 0);
        }

        [Fact]
        public async Task Bootstrap_leaves_a_complete_history_so_apply_can_continue_from_it()
        {
            var connectionString = await _fixture.CreateEmptyDatabaseAsync();
            await using var services = BuildServices(connectionString, "LocalDocker");

            Assert.Equal(0, await SqlCli.RunAsync("bootstrap", services));

            using var scope = services.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<SqlScriptRunner>();

            // Skipped scripts are recorded too — otherwise they would read as a "gap" and `apply`
            // would refuse to move the database forward ever again.
            var status = await runner.GetStatusAsync();
            Assert.Empty(status.Pending);

            var apply = await runner.ApplyAsync(baselineOnly: false);
            Assert.True(apply.Success, apply.Error);
            Assert.Empty(apply.Ran);
        }

        [Fact]
        public async Task Bootstrap_refuses_a_database_that_already_has_history()
        {
            var connectionString = await _fixture.CreateEmptyDatabaseAsync();
            await using var services = BuildServices(connectionString, "LocalDocker");

            Assert.Equal(0, await SqlCli.RunAsync("bootstrap", services));

            // Re-running would replay every seed script over real rows.
            Assert.Equal(1, await SqlCli.RunAsync("bootstrap", services));
        }

        [Fact]
        public async Task Bootstrap_refuses_a_remote_database_without_connecting_to_it()
        {
            // A host that does not resolve: if the guard ever stopped short-circuiting, this test
            // would fail on a connection error rather than pass for the wrong reason.
            const string remote = "Host=db.invalid.example;Port=5432;Database=postgres;Username=u;Password=p";
            await using var services = BuildServices(remote, "Supabase");

            Assert.Equal(1, await SqlCli.RunAsync("bootstrap", services));
        }

        [Fact]
        public async Task Bootstrap_is_refused_when_DB_PROFILE_contradicts_the_connection_string()
        {
            var connectionString = await _fixture.CreateEmptyDatabaseAsync();

            // Declaring Supabase while pointing at a local database fails at wiring time — before
            // anything can run against the wrong database.
            var ex = Assert.Throws<InvalidOperationException>(() => BuildServices(connectionString, "Supabase"));
            Assert.Contains("DB_PROFILE", ex.Message);
        }

        [Fact]
        public async Task An_EF_migrated_database_is_not_considered_fresh_enough_to_skip_the_seed_scripts()
        {
            // Guards the ordering inside bootstrap: migrations first, seed scripts second. If the
            // scripts ran before the schema existed they would all fail; if migrations ran twice the
            // second call would be a no-op, not an error — so assert on the outcome instead.
            var connectionString = await _fixture.CreateEmptyDatabaseAsync();

            await using (var context = FreshDatabaseFixture.CreateContext(connectionString))
            {
                await context.Database.MigrateAsync();
            }

            Assert.Equal(0L, await CountAsync(connectionString, "exam_types"));

            await using var services = BuildServices(connectionString, "LocalDocker");
            Assert.Equal(0, await SqlCli.RunAsync("bootstrap", services));

            Assert.True(await CountAsync(connectionString, "exam_types") > 0);
        }

        [Fact]
        public void The_bootstrap_skip_list_only_names_scripts_that_exist()
        {
            var source = new EmbeddedSqlScriptSource();
            var names = source.GetScripts().Select(s => s.Name).ToHashSet(StringComparer.Ordinal);

            var skips = source.GetBootstrapSkips();

            Assert.All(skips, skip => Assert.Contains(skip, names));
            Assert.Contains("schema.sql", skips);
        }

        [Fact]
        public async Task Bootstrap_records_skipped_scripts_without_running_them()
        {
            var connectionString = await _fixture.CreateEmptyDatabaseAsync();
            await using var services = BuildServices(connectionString, "LocalDocker");

            Assert.Equal(0, await SqlCli.RunAsync("bootstrap", services));

            var skipped = await FreshDatabaseFixture.ScalarAsync<long>(connectionString,
                $"SELECT count(*) FROM {SqlScriptRunner.TrackingTable} WHERE note = 'bootstrap-skipped'");
            var expected = new EmbeddedSqlScriptSource().GetBootstrapSkips().Count;

            Assert.Equal(expected, skipped);
        }

        /// <summary>
        /// ReferenceDataSync's table whitelist is a plain string list that nothing compiles — a renamed
        /// or misspelled table would only surface mid-sync, after the TRUNCATE step has already emptied
        /// the local ones. Check the names against a real schema.
        /// </summary>
        [Fact]
        public async Task The_reference_sync_whitelist_only_names_tables_that_exist()
        {
            var connectionString = await _fixture.CreateEmptyDatabaseAsync();
            await using (var context = FreshDatabaseFixture.CreateContext(connectionString))
            {
                await context.Database.MigrateAsync();
            }

            var listed = ReferenceDataSync.DefaultTables.Append(ReferenceDataSync.UsersTable);

            foreach (var table in listed)
            {
                var exists = await FreshDatabaseFixture.ScalarAsync<bool>(
                    connectionString, $"SELECT to_regclass('public.{table}') IS NOT NULL");
                Assert.True(exists, $"ReferenceDataSync lists '{table}', which is not a table in the schema.");
            }

            // Per-learner data must stay out: keeping what a practice session produces local and
            // disposable is the point. (questions/users ARE pulled — the question bank is shared
            // reference data, and users only comes along because questions.created_by needs it.)
            Assert.DoesNotContain("submissions", ReferenceDataSync.DefaultTables);
            Assert.DoesNotContain("weak_points", ReferenceDataSync.DefaultTables);
            Assert.DoesNotContain("ai_call_logs", ReferenceDataSync.DefaultTables);
        }

        /// <summary>
        /// The tables the local database CANNOT get from the seed scripts, pinned so the gap is a
        /// documented fact rather than a surprise. Both are populated at runtime, not by any .sql
        /// file: question_bank_categories through the API, users by EnsureUserProfileMiddleware on
        /// the first authenticated request (which is also why users.password_hash stays empty — see
        /// JwtAuthenticationTests). Copying either one out of Supabase is a separate, explicit act
        /// (scripts/sync-supabase-reference-data.ps1), never part of bootstrap.
        /// </summary>
        [Fact]
        public async Task Bootstrap_leaves_the_runtime_owned_tables_empty()
        {
            var connectionString = await _fixture.CreateEmptyDatabaseAsync();
            await using var services = BuildServices(connectionString, "LocalDocker");

            Assert.Equal(0, await SqlCli.RunAsync("bootstrap", services));

            Assert.Equal(0L, await CountAsync(connectionString, "question_bank_categories"));
            Assert.Equal(0L, await CountAsync(connectionString, "users"));
        }
    }
}
