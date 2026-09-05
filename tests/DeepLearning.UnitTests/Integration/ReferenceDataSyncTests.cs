using DeepLearning.Infrastructure;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace DeepLearning.UnitTests.Integration
{
    /// <summary>
    /// Copying the reference tables out of the shared database instead of re-deriving them from the
    /// seed scripts.
    ///
    /// <para>The scenario each test sets up is the real one: a "shared" database that was seeded from
    /// the scripts and then EDITED BY HAND afterwards (dimension weights, error taxonomy wording,
    /// prompt rows, categories created through the API), and a brand-new empty local database. What
    /// has to come out the other end is the shared database's actual content — including the edits
    /// that exist in no <c>.sql</c> file, which is precisely what <c>sql bootstrap</c> cannot give
    /// you.</para>
    ///
    /// <para>Both databases here are throwaway containers. Nothing in this suite ever talks to the
    /// real Supabase project.</para>
    /// </summary>
    [Collection(FreshDatabaseCollection.Name)]
    public class ReferenceDataSyncTests
    {
        private readonly FreshDatabaseFixture _fixture;

        public ReferenceDataSyncTests(FreshDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        private ReferenceDataSync SyncInto(string targetConnectionString)
            => new(
                DatabaseTargetResolver.Resolve("LocalDocker", targetConnectionString),
                targetConnectionString,
                NullLogger<ReferenceDataSync>.Instance);

        /// <summary>A migrated + bootstrapped database standing in for the shared one.</summary>
        private async Task<string> CreateSharedLikeDatabaseAsync()
        {
            var connectionString = await _fixture.CreateEmptyDatabaseAsync();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["DB_PROFILE"] = "LocalDocker",
                })
                .Build();

            await using var services = new ServiceCollection().AddLogging().AddInfrastructure(configuration).BuildServiceProvider();
            Assert.Equal(0, await DeepLearning.Api.SqlCli.RunAsync("bootstrap", services));

            return connectionString;
        }

        private static async Task ExecuteAsync(string connectionString, string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static Task<long> CountAsync(string connectionString, string table)
            => FreshDatabaseFixture.ScalarAsync<long>(connectionString, $"SELECT count(*) FROM {table}");

        [Fact]
        public async Task Hand_edits_that_exist_in_no_sql_script_are_carried_over()
        {
            var shared = await CreateSharedLikeDatabaseAsync();

            // The edits the seed scripts know nothing about — the whole reason for copying rather than
            // replaying. A category row in particular can ONLY get here this way: no script inserts one.
            await ExecuteAsync(shared, """
                UPDATE assessment_dimensions SET dimension_name = '手改过的维度名', pass_threshold = 'B4';
                UPDATE error_taxonomies SET description = '手改过的错误说明';
                INSERT INTO question_bank_categories (category_type, name, description)
                VALUES ('domain', '手建的分类', '通过 API 建的，任何 .sql 里都没有');
                """);

            var target = await _fixture.CreateEmptyDatabaseAsync();
            await using (var context = FreshDatabaseFixture.CreateContext(target))
            {
                await context.Database.MigrateAsync();
            }

            var report = await SyncInto(target).CopyAsync(shared, ReferenceDataSync.DefaultTables);
            Assert.True(report.Success, report.Error);

            Assert.Equal("手改过的维度名", await FreshDatabaseFixture.ScalarAsync<string>(
                target, "SELECT DISTINCT dimension_name FROM assessment_dimensions"));
            Assert.Equal("手改过的错误说明", await FreshDatabaseFixture.ScalarAsync<string>(
                target, "SELECT DISTINCT description FROM error_taxonomies"));
            Assert.Equal("手建的分类", await FreshDatabaseFixture.ScalarAsync<string>(
                target, "SELECT name FROM question_bank_categories"));
        }

        [Fact]
        public async Task Every_reference_table_ends_up_with_the_same_rows_as_the_source()
        {
            var shared = await CreateSharedLikeDatabaseAsync();
            var target = await _fixture.CreateEmptyDatabaseAsync();
            await using (var context = FreshDatabaseFixture.CreateContext(target))
            {
                await context.Database.MigrateAsync();
            }

            var report = await SyncInto(target).CopyAsync(shared, ReferenceDataSync.DefaultTables);
            Assert.True(report.Success, report.Error);

            foreach (var table in ReferenceDataSync.DefaultTables)
            {
                var expected = await CountAsync(shared, table);
                Assert.Equal(expected, await CountAsync(target, table));
                Assert.Equal(expected, report.Copied.Single(c => c.Table == table).Rows);
            }

            // Not just counts: JSONB, enums and timestamps have to survive the binary COPY unchanged.
            // md5 over an ordered projection catches a value that arrives subtly re-formatted.
            const string digest = """
                SELECT md5(string_agg(t::text, '|' ORDER BY t::text))
                FROM (SELECT * FROM prompt_templates) t
                """;
            Assert.Equal(
                await FreshDatabaseFixture.ScalarAsync<string>(shared, digest),
                await FreshDatabaseFixture.ScalarAsync<string>(target, digest));
        }

        [Fact]
        public async Task A_self_referencing_category_tree_survives_the_copy()
        {
            // question_bank_categories.parent_id points at the same table, so a child row can arrive
            // before its parent. Nothing about COPY's row order guarantees otherwise — this is why the
            // load disables FK triggers for the session rather than ordering tables cleverly.
            var shared = await CreateSharedLikeDatabaseAsync();
            await ExecuteAsync(shared, """
                WITH parent AS (
                    INSERT INTO question_bank_categories (category_type, name) VALUES ('domain', '医疗')
                    RETURNING id
                )
                INSERT INTO question_bank_categories (category_type, name, parent_id)
                SELECT 'domain', '公共卫生', id FROM parent;
                """);

            var target = await _fixture.CreateEmptyDatabaseAsync();
            await using (var context = FreshDatabaseFixture.CreateContext(target))
            {
                await context.Database.MigrateAsync();
            }

            var report = await SyncInto(target).CopyAsync(shared, ReferenceDataSync.DefaultTables);
            Assert.True(report.Success, report.Error);

            Assert.Equal("医疗", await FreshDatabaseFixture.ScalarAsync<string>(target, """
                SELECT parent.name
                FROM question_bank_categories child
                JOIN question_bank_categories parent ON parent.id = child.parent_id
                WHERE child.name = '公共卫生'
                """));
        }

        [Fact]
        public async Task Re_running_the_sync_replaces_rather_than_duplicates()
        {
            var shared = await CreateSharedLikeDatabaseAsync();
            var target = await _fixture.CreateEmptyDatabaseAsync();
            await using (var context = FreshDatabaseFixture.CreateContext(target))
            {
                await context.Database.MigrateAsync();
            }

            var sync = SyncInto(target);
            Assert.True((await sync.CopyAsync(shared, ReferenceDataSync.DefaultTables)).Success);
            var afterFirst = await CountAsync(target, "assessment_dimensions");

            // A row deleted upstream between the two runs must be gone here too — the local copy is a
            // mirror, not an accumulation.
            await ExecuteAsync(shared, "DELETE FROM llm_provider_models WHERE model = 'claude-sonnet-5'");

            Assert.True((await sync.CopyAsync(shared, ReferenceDataSync.DefaultTables)).Success);

            Assert.Equal(afterFirst, await CountAsync(target, "assessment_dimensions"));
            Assert.Equal(0L, await FreshDatabaseFixture.ScalarAsync<long>(
                target, "SELECT count(*) FROM llm_provider_models WHERE model = 'claude-sonnet-5'"));
        }

        [Fact]
        public async Task Questions_bring_their_users_along_so_created_by_never_dangles()
        {
            // questions.created_by -> users(id). The load runs with FK triggers off, so leaving users out
            // would NOT error — it would leave created_by pointing at ids that do not exist here, and the
            // damage would only show up later as a null author. Expand() closes the set instead.
            var shared = await CreateSharedLikeDatabaseAsync();
            await ExecuteAsync(shared, """
                WITH u AS (
                    INSERT INTO users (id, username, email) VALUES (gen_random_uuid(), 'author', 'author@example.com')
                    RETURNING id
                )
                INSERT INTO questions (task_type, difficulty, title, source_text, origin, source_type, created_by)
                SELECT 'A', 'medium', '线上题目', 'Some source text.', 'ai_generated', 'ai_generated', id FROM u;
                """);

            var target = await _fixture.CreateEmptyDatabaseAsync();
            await using (var context = FreshDatabaseFixture.CreateContext(target))
            {
                await context.Database.MigrateAsync();
            }

            // Ask for questions ONLY — users must be pulled in regardless.
            var report = await SyncInto(target).CopyAsync(shared, ["questions"]);
            Assert.True(report.Success, report.Error);
            Assert.Contains(ReferenceDataSync.UsersTable, report.Copied.Select(c => c.Table));

            Assert.Equal(1L, await CountAsync(target, "questions"));
            Assert.Equal(0L, await FreshDatabaseFixture.ScalarAsync<long>(target, """
                SELECT count(*) FROM questions q
                WHERE q.created_by IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM users u WHERE u.id = q.created_by)
                """));

            // password_hash stays null: it is vestigial, and inventing a value would imply local logins
            // work differently than they do (Supabase Auth owns credentials).
            Assert.Equal(0L, await FreshDatabaseFixture.ScalarAsync<long>(
                target, "SELECT count(*) FROM users WHERE password_hash IS NOT NULL"));
        }

        [Fact]
        public void Expand_closes_the_dependency_chain_transitively()
        {
            // question_category_map -> questions -> users. A one-level expansion would stop at
            // questions and leave created_by pointing at ids that are not here.
            var expanded = ReferenceDataSync.Expand(["question_category_map"]);

            Assert.Contains("question_category_map", expanded);
            Assert.Contains("questions", expanded);
            Assert.Contains("users", expanded);
            Assert.Contains("question_bank_categories", expanded);
            Assert.Equal(expanded.Distinct().Count(), expanded.Count);
        }

        [Fact]
        public async Task The_whole_question_bank_including_its_child_rows_comes_across()
        {
            var shared = await CreateSharedLikeDatabaseAsync();
            await ExecuteAsync(shared, """
                WITH u AS (
                    INSERT INTO users (id, username, email) VALUES (gen_random_uuid(), 'author2', 'a2@example.com')
                    RETURNING id
                ), q AS (
                    INSERT INTO questions (task_type, difficulty, title, source_text, flawed_translation_text,
                                           origin, source_type, created_by, in_bank)
                    SELECT 'B', 'hard', 'TaskB 题', 'Source text for task B.', '有瑕疵的译文', 'ai_generated',
                           'ai_generated', id, true FROM u
                    RETURNING id
                ), c AS (
                    INSERT INTO question_bank_categories (category_type, name) VALUES ('domain', '法律')
                    RETURNING id
                ), ck AS (
                    INSERT INTO meaning_checkpoints (question_id, checkpoint_text, importance)
                    SELECT id, '必须译出的要点', 'core' FROM q RETURNING id
                ), rt AS (
                    INSERT INTO reference_translations (question_id, reference_text)
                    SELECT id, '参考译文' FROM q RETURNING id
                ), sb AS (
                    INSERT INTO task_b_seeded_errors (question_id, position_start, position_end,
                                                      error_taxonomy_id, correct_reference_text)
                    SELECT q.id, 0, 5, (SELECT id FROM error_taxonomies LIMIT 1), '正确说法' FROM q RETURNING id
                ), map AS (
                    INSERT INTO question_category_map (question_id, category_id)
                    SELECT q.id, c.id FROM q, c RETURNING id
                )
                INSERT INTO seed_reference_links (generated_question_id, seed_question_id, similarity_reason)
                SELECT q.id, q.id, '自引用，仅用于测试外键' FROM q;
                """);

            var target = await _fixture.CreateEmptyDatabaseAsync();
            await using (var context = FreshDatabaseFixture.CreateContext(target))
            {
                await context.Database.MigrateAsync();
            }

            var report = await SyncInto(target).CopyAsync(shared, ReferenceDataSync.DefaultTables);
            Assert.True(report.Success, report.Error);

            foreach (var table in new[]
                     {
                         "meaning_checkpoints", "task_b_seeded_errors", "reference_translations",
                         "question_category_map", "seed_reference_links",
                     })
            {
                Assert.Equal(await CountAsync(shared, table), await CountAsync(target, table));
                Assert.True(await CountAsync(target, table) > 0, $"{table} came across empty");
            }

            // No child row may point at a question that did not come with it.
            Assert.Equal(0L, await FreshDatabaseFixture.ScalarAsync<long>(target, """
                SELECT (SELECT count(*) FROM meaning_checkpoints c
                        WHERE NOT EXISTS (SELECT 1 FROM questions q WHERE q.id = c.question_id))
                     + (SELECT count(*) FROM task_b_seeded_errors e
                        WHERE NOT EXISTS (SELECT 1 FROM questions q WHERE q.id = e.question_id))
                     + (SELECT count(*) FROM question_category_map m
                        WHERE NOT EXISTS (SELECT 1 FROM question_bank_categories c WHERE c.id = m.category_id))
                """));
        }

        [Fact]
        public async Task The_default_set_carries_the_question_bank_but_no_per_learner_data()
        {
            Assert.Contains("questions", ReferenceDataSync.DefaultTables);
            Assert.Contains("users", ReferenceDataSync.DefaultTables);

            // The tables that hold what a practice session produces stay local and disposable.
            foreach (var businessTable in new[]
                     { "submissions", "weak_points", "grading_summaries", "follow_up_questions", "ai_call_logs" })
            {
                Assert.DoesNotContain(businessTable, ReferenceDataSync.DefaultTables);
            }
        }

        [Fact]
        public async Task The_source_database_is_never_written_to()
        {
            // The one guarantee that matters when a temp database is in play: pulling from Supabase must
            // not change Supabase. The source session is opened read-only, so this holds even if a future
            // edit adds a write — but assert the observable outcome, not the mechanism.
            var shared = await CreateSharedLikeDatabaseAsync();

            const string digest = """
                SELECT md5(string_agg(x, '|' ORDER BY x)) FROM (
                    SELECT t::text AS x FROM prompt_templates t
                    UNION ALL SELECT t::text FROM assessment_dimensions t
                    UNION ALL SELECT t::text FROM error_taxonomies t
                    UNION ALL SELECT t::text FROM llm_provider_models t
                ) rows
                """;
            var before = await FreshDatabaseFixture.ScalarAsync<string>(shared, digest);
            var sqlHistoryBefore = await CountAsync(shared, "_sql_scripts");

            var target = await _fixture.CreateEmptyDatabaseAsync();
            await using (var context = FreshDatabaseFixture.CreateContext(target))
            {
                await context.Database.MigrateAsync();
            }
            Assert.True((await SyncInto(target).CopyAsync(shared, ReferenceDataSync.DefaultTables)).Success);

            Assert.Equal(before, await FreshDatabaseFixture.ScalarAsync<string>(shared, digest));
            Assert.Equal(sqlHistoryBefore, await CountAsync(shared, "_sql_scripts"));
        }

        [Fact]
        public async Task A_write_attempted_on_the_source_connection_would_be_rejected_by_the_server()
        {
            // Pins the mechanism behind the test above: the source session is read-only at the server, so
            // the protection does not depend on every future code path remembering to only SELECT.
            var shared = await CreateSharedLikeDatabaseAsync();

            await using var connection = new NpgsqlConnection(shared);
            await connection.OpenAsync();
            await using (var readOnly = new NpgsqlCommand("SET default_transaction_read_only = on", connection))
            {
                await readOnly.ExecuteNonQueryAsync();
            }

            await using var write = new NpgsqlCommand("DELETE FROM llm_provider_models", connection);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => write.ExecuteNonQueryAsync());
            Assert.Equal("25006", ex.SqlState); // read_only_sql_transaction
        }

        [Fact]
        public async Task A_pull_only_container_still_ends_up_with_a_usable_script_history()
        {
            // Pulling gives a database the END STATE of every .sql script (that is what the shared
            // database is), but leaves _sql_scripts empty — and `sql apply` refuses without a baseline.
            // A container prepared with pull-reference alone must not be a dead end.
            var shared = await CreateSharedLikeDatabaseAsync();
            var target = await _fixture.CreateEmptyDatabaseAsync();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = target,
                    ["ConnectionStrings:" + DeepLearning.Api.DbCli.SourceConnectionStringName] = shared,
                    ["DB_PROFILE"] = "LocalDocker",
                })
                .Build();
            // DbCli reads IConfiguration out of DI, the way the real host provides it.
            await using var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IConfiguration>(configuration)
                .AddInfrastructure(configuration)
                .BuildServiceProvider();

            Assert.Equal(0, await DeepLearning.Api.DbCli.RunAsync("pull-reference", [], services));

            using var scope = services.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<
                DeepLearning.Infrastructure.Persistence.Sql.SqlScriptRunner>();

            Assert.Empty((await runner.GetStatusAsync()).Pending);
            var apply = await runner.ApplyAsync(baselineOnly: false);
            Assert.True(apply.Success, apply.Error);
            Assert.Empty(apply.Ran);
        }

        [Fact]
        public async Task Syncing_into_a_remote_database_is_refused()
        {
            var shared = await CreateSharedLikeDatabaseAsync();

            // Destination declared (and resolved) as remote — the one direction that must be impossible,
            // because the copy truncates what it replaces.
            const string remote = "Host=db.invalid.example;Port=5432;Database=postgres;Username=u;Password=p";
            var sync = new ReferenceDataSync(
                DatabaseTargetResolver.Resolve("Supabase", remote), remote, NullLogger<ReferenceDataSync>.Instance);

            var report = await sync.CopyAsync(shared, ReferenceDataSync.DefaultTables);

            Assert.False(report.Success);
            Assert.Contains("must be a database on this machine", report.Error);
            Assert.Empty(report.Copied);
        }

        [Fact]
        public async Task Syncing_a_database_from_itself_is_refused()
        {
            // The "I pasted the destination's own connection string into --source" mistake. Truncate
            // then copy-from-self would empty the reference tables, so it has to be caught before the
            // truncate rather than reported afterwards.
            var target = await CreateSharedLikeDatabaseAsync();

            var report = await SyncInto(target).CopyAsync(target, ReferenceDataSync.DefaultTables);

            Assert.False(report.Success);
            Assert.Contains("same database", report.Error);
            Assert.True(await CountAsync(target, "exam_types") > 0);
        }

        [Fact]
        public async Task A_table_missing_upstream_fails_before_anything_is_truncated()
        {
            var shared = await CreateSharedLikeDatabaseAsync();
            var target = await _fixture.CreateEmptyDatabaseAsync();
            await using (var context = FreshDatabaseFixture.CreateContext(target))
            {
                await context.Database.MigrateAsync();
            }

            // Seed the destination so "nothing was truncated" is observable.
            await ExecuteAsync(target, """
                INSERT INTO exam_types (code, name, subject_category, source_language, target_language)
                VALUES ('local_only', '本地占位', 'translation', 'en', 'zh');
                """);

            var report = await SyncInto(target).CopyAsync(
                shared, [.. ReferenceDataSync.DefaultTables, "table_that_does_not_exist"]);

            Assert.False(report.Success);
            Assert.Contains("does not exist in the source database", report.Error);
            Assert.Equal(1L, await CountAsync(target, "exam_types"));
        }

        [Fact]
        public async Task A_column_that_exists_only_upstream_is_skipped_and_reported()
        {
            var shared = await CreateSharedLikeDatabaseAsync();
            var target = await _fixture.CreateEmptyDatabaseAsync();
            await using (var context = FreshDatabaseFixture.CreateContext(target))
            {
                await context.Database.MigrateAsync();
            }

            // Schema drift in the harmless direction: upstream has a column this schema does not. The
            // copy proceeds on the shared columns, but must say so rather than quietly dropping data.
            await ExecuteAsync(shared, "ALTER TABLE exam_types ADD COLUMN experimental_note text");
            await ExecuteAsync(shared, "UPDATE exam_types SET experimental_note = 'x'");

            var report = await SyncInto(target).CopyAsync(shared, ["exam_types"]);

            Assert.True(report.Success, report.Error);
            Assert.Contains("experimental_note", report.Copied.Single().IgnoredSourceColumns);
            Assert.Equal(await CountAsync(shared, "exam_types"), await CountAsync(target, "exam_types"));
        }

        [Fact]
        public async Task A_required_column_missing_upstream_stops_the_sync()
        {
            var shared = await CreateSharedLikeDatabaseAsync();
            var target = await _fixture.CreateEmptyDatabaseAsync();
            await using (var context = FreshDatabaseFixture.CreateContext(target))
            {
                await context.Database.MigrateAsync();
            }

            // Drift in the dangerous direction: this schema needs a value the source cannot supply.
            await ExecuteAsync(target, "ALTER TABLE exam_types ADD COLUMN required_locally text NOT NULL");

            var report = await SyncInto(target).CopyAsync(shared, ["exam_types"]);

            Assert.False(report.Success);
            Assert.Contains("required_locally", report.Error);
            Assert.Contains("diverged", report.Error);
        }
    }
}
