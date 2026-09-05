using Microsoft.Extensions.Logging;
using Npgsql;

namespace DeepLearning.Infrastructure.Persistence
{
    public sealed record ReferenceTableCopy(string Table, long Rows, IReadOnlyList<string> IgnoredSourceColumns);

    public sealed record ReferenceSyncReport(IReadOnlyList<ReferenceTableCopy> Copied, string? FailedTable, string? Error)
    {
        public bool Success => Error is null;
    }

    /// <summary>
    /// Copies the reference/config tables out of the shared Supabase database into a local throwaway one,
    /// row for row.
    ///
    /// <para><b>Why this exists instead of just replaying the seed scripts.</b> The shared database is not
    /// the sum of <c>Persistence/Sql/*.sql</c> — dimensions, error taxonomies and prompt rows have been
    /// edited by hand and through the API since they were seeded (SqlScriptRunner's own safety contract is
    /// built around that fact). Replaying the scripts therefore produces a database that is internally
    /// valid but subtly different from the one the app actually behaves against, which is the worst kind of
    /// test environment: close enough to trust, wrong where it matters. <c>sql bootstrap</c> is still the
    /// right answer offline and in CI; when the shared database is reachable, copying it is strictly
    /// better.</para>
    ///
    /// <para><b>Direction is structural, not documented.</b> The destination is always the connection this
    /// process is already configured with — which <see cref="DatabaseTargetResolver"/> has already proven
    /// is on this machine — and the source must be remote. There is no argument combination that writes to
    /// Supabase (AGENTS.md #4).</para>
    ///
    /// <para>Transfer uses PostgreSQL's binary COPY, streamed straight from one connection to the other,
    /// so no value is ever parsed, re-formatted or type-mapped in between — enums, JSONB and timestamps
    /// arrive exactly as stored. Columns are named explicitly on both sides rather than relying on
    /// matching physical column order, which two independently-evolved databases have no reason to share.</para>
    /// </summary>
    public sealed class ReferenceDataSync
    {
        /// <summary>
        /// What a fresh local database gets: the reference/config tables an app instance reads but rarely
        /// writes, plus the question bank.
        ///
        /// <para>The per-learner business tables — submissions, weak_points, grading_summaries,
        /// follow_up_*, progress_* — are deliberately absent. Those are the throwaway test data; keeping
        /// them local and disposable is the entire point.</para>
        ///
        /// <para><c>users</c> is here only because <c>questions.created_by</c> points at it. It is not
        /// needed for logging in: the backend never issues or checks credentials (Supabase Auth does), and
        /// EnsureUserProfileMiddleware creates the local row from the validated JWT on that user's first
        /// request, with <c>password_hash</c> left empty because nothing reads it.</para>
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultTables =
        [
            "exam_types",
            "assessment_dimensions",
            "error_taxonomies",
            "question_bank_categories",
            "generation_policy",
            "prompt_templates",
            "llm_provider_settings",
            "llm_provider_models",
            "ai_operation_provider_overrides",
            // The two-level weak-point taxonomy (薄弱点分类与生命周期管理_策划书.md §1) — global,
            // not per-exam-type, but still hand-curated/reviewed reference data (proposed leaves
            // get approved/merged through the admin surface) rather than per-learner business data.
            "weak_point_categories",
            "weak_point_catalog",
            "users",
            "questions",
            // The question bank's own child rows. A question without these is not a lighter copy of
            // the real thing, it is a different thing: no category to browse it under, a Task B item
            // with no planted errors to find, and — because GradeSubmissionCommandHandler reads
            // meaning_checkpoints — grading that scores against a different set of required meanings.
            "meaning_checkpoints",
            "task_b_seeded_errors",
            "reference_translations",
            "question_category_map",
            "seed_reference_links",
        ];

        public const string UsersTable = "users";
        public const string QuestionsTable = "questions";

        /// <summary>
        /// Tables that cannot be copied without their foreign-key targets. Loading runs with FK triggers
        /// off (see <see cref="CopyAsync"/>), so omitting a parent would not error — it would quietly leave
        /// rows pointing at ids that do not exist locally, which surfaces much later as a null navigation
        /// property. <see cref="Expand"/> closes the set instead (AGENTS.md #1: validate, don't hope).
        /// </summary>
        private static readonly Dictionary<string, string[]> RequiredCompanions = new(StringComparer.Ordinal)
        {
            ["weak_point_catalog"] = ["weak_point_categories"],               // .category_id (nullable, but a dangling id is still wrong)
            [QuestionsTable] = [UsersTable],                                  // questions.created_by
            ["meaning_checkpoints"] = [QuestionsTable],                       // .question_id
            ["reference_translations"] = [QuestionsTable],                    // .question_id
            ["seed_reference_links"] = [QuestionsTable],                      // .generated_question_id, .seed_question_id
            ["task_b_seeded_errors"] = [QuestionsTable, "error_taxonomies"],  // .question_id, .error_taxonomy_id
            ["question_category_map"] = [QuestionsTable, "question_bank_categories"],
        };

        /// <summary>
        /// Adds every table the requested ones depend on, preserving <see cref="DefaultTables"/> order for
        /// the ones that appear there so output stays predictable.
        /// </summary>
        public static IReadOnlyList<string> Expand(IEnumerable<string> requested)
        {
            // Transitive, not one level: question_category_map needs questions, which needs users.
            // A single pass would pull in questions and stop, leaving created_by dangling — the exact
            // failure this method exists to prevent.
            var wanted = new HashSet<string>(requested, StringComparer.Ordinal);
            var pending = new Queue<string>(wanted);
            while (pending.Count > 0)
            {
                if (!RequiredCompanions.TryGetValue(pending.Dequeue(), out var companions))
                {
                    continue;
                }

                foreach (var companion in companions.Where(c => wanted.Add(c)))
                {
                    pending.Enqueue(companion);
                }
            }

            return
            [
                .. DefaultTables.Where(wanted.Contains),
                .. wanted.Except(DefaultTables, StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal),
            ];
        }

        private readonly DatabaseTarget _target;
        private readonly string _targetConnectionString;
        private readonly ILogger<ReferenceDataSync> _logger;

        public ReferenceDataSync(DatabaseTarget target, string targetConnectionString, ILogger<ReferenceDataSync> logger)
        {
            _target = target;
            _targetConnectionString = targetConnectionString;
            _logger = logger;
        }

        public async Task<ReferenceSyncReport> CopyAsync(
            string sourceConnectionString,
            IReadOnlyList<string> tables,
            CancellationToken cancellationToken = default)
        {
            var copied = new List<ReferenceTableCopy>();

            if (!_target.IsLocal)
            {
                return new ReferenceSyncReport(copied, null,
                    $"refusing to sync into {_target.Describe()}: the destination must be a database on this machine. " +
                    "This command truncates the tables it replaces.");
            }

            // The mistake this catches is pasting the destination's own connection string into --source:
            // the tables would be truncated and then "copied" from themselves, i.e. emptied. Checking
            // for the same database is exact; checking merely for "source is local" would also block
            // legitimate cases (a local restore of a Supabase dump) while catching nothing extra —
            // the destination is already pinned to this process's own connection.
            var sourceTarget = DatabaseTargetResolver.Resolve(null, sourceConnectionString);
            if (string.Equals(sourceTarget.Host, _target.Host, StringComparison.OrdinalIgnoreCase)
                && sourceTarget.Port == _target.Port
                && string.Equals(sourceTarget.Database, _target.Database, StringComparison.Ordinal))
            {
                return new ReferenceSyncReport(copied, null,
                    $"refusing to sync: the source and the destination are the same database ({sourceTarget.Describe()}). " +
                    "This command truncates before it copies, so that would empty the reference tables.");
            }

            if (tables.Count == 0)
            {
                return new ReferenceSyncReport(copied, null, "refusing to sync: no tables requested.");
            }

            tables = Expand(tables);

            await using var source = new NpgsqlConnection(sourceConnectionString);
            await using var destination = new NpgsqlConnection(_targetConnectionString);
            await source.OpenAsync(cancellationToken);
            await destination.OpenAsync(cancellationToken);

            // Make the source physically incapable of being written to for the life of this connection.
            // The code below only ever reads it, but "only ever reads it" is a property of today's code;
            // this makes it a property of the session, so a future edit that adds a write here fails
            // against the shared database instead of succeeding (AGENTS.md #4).
            await using (var readOnly = new NpgsqlCommand("SET default_transaction_read_only = on", source))
            {
                await readOnly.ExecuteNonQueryAsync(cancellationToken);
            }

            // Foreign keys are checked per row during COPY, and question_bank_categories references
            // itself (parent_id), so no table ordering makes an arbitrary row order safe. Turning off
            // triggers for this session is the standard bulk-load answer; it needs superuser, which the
            // local docker postgres role is and a Supabase one is not — another reason the destination
            // can only ever be local.
            await using (var replicaRole = new NpgsqlCommand("SET session_replication_role = replica", destination))
            {
                await replicaRole.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var transaction = await destination.BeginTransactionAsync(cancellationToken);

            try
            {
                var columnsPerTable = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
                foreach (var table in tables)
                {
                    var sourceColumns = await ReadColumnsAsync(source, table, cancellationToken);
                    if (sourceColumns.Count == 0)
                    {
                        return new ReferenceSyncReport(copied, table,
                            $"table '{table}' does not exist in the source database.");
                    }

                    var destinationColumns = await ReadColumnsAsync(destination, table, cancellationToken);
                    if (destinationColumns.Count == 0)
                    {
                        return new ReferenceSyncReport(copied, table,
                            $"table '{table}' does not exist in the local database — run `sql bootstrap` first.");
                    }

                    // A local column the source cannot fill is only a problem when the row could not be
                    // inserted without it. Hard-fail there rather than producing a database that is
                    // missing data nobody notices (AGENTS.md #1).
                    var missingLocally = destinationColumns
                        .Where(c => !sourceColumns.ContainsKey(c.Key))
                        .Where(c => c.Value.IsRequired)
                        .Select(c => c.Key)
                        .ToList();
                    if (missingLocally.Count > 0)
                    {
                        return new ReferenceSyncReport(copied, table,
                            $"'{table}' has NOT NULL column(s) with no default that the source does not have: " +
                            $"{string.Join(", ", missingLocally)}. The two schemas have diverged — reconcile them first.");
                    }

                    columnsPerTable[table] = destinationColumns.Keys.Where(sourceColumns.ContainsKey).ToList();
                }

                // One statement so the CASCADE closure is computed once; on a throwaway database the
                // business rows it takes with it are exactly the test data this whole setup exists to discard.
                await using (var truncate = new NpgsqlCommand(
                    $"TRUNCATE {string.Join(", ", tables.Select(Quote))} CASCADE", destination, transaction))
                {
                    await truncate.ExecuteNonQueryAsync(cancellationToken);
                }

                foreach (var table in tables)
                {
                    var columns = columnsPerTable[table];
                    var columnList = string.Join(", ", columns.Select(Quote));

                    // One BeginRawBinaryCopyAsync per side; the direction comes from the statement
                    // (TO STDOUT reads, FROM STDIN writes). "Raw" means Npgsql moves the COPY BINARY
                    // bytes through untouched — no per-value decode/encode, so there is nothing for a
                    // type mapping to get wrong, and the streams can simply be piped together.
                    await using (var reader = await source.BeginRawBinaryCopyAsync(
                        $"COPY (SELECT {columnList} FROM {Quote(table)}) TO STDOUT (FORMAT BINARY)", cancellationToken))
                    await using (var writer = await destination.BeginRawBinaryCopyAsync(
                        $"COPY {Quote(table)} ({columnList}) FROM STDIN (FORMAT BINARY)", cancellationToken))
                    {
                        await reader.CopyToAsync(writer, cancellationToken);
                    }

                    await using var count = new NpgsqlCommand($"SELECT count(*) FROM {Quote(table)}", destination, transaction);
                    var rows = (long)(await count.ExecuteScalarAsync(cancellationToken))!;

                    var ignored = (await ReadColumnsAsync(source, table, cancellationToken))
                        .Keys.Where(c => !columns.Contains(c, StringComparer.Ordinal)).ToList();

                    copied.Add(new ReferenceTableCopy(table, rows, ignored));
                    _logger.LogInformation("Reference table copied: {Table} ({Rows} rows)", table, rows);
                }

                await transaction.CommitAsync(cancellationToken);
                return new ReferenceSyncReport(copied, null, null);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _logger.LogError(ex, "Reference data sync failed");
                return new ReferenceSyncReport(copied, copied.Count < tables.Count ? tables[copied.Count] : null, ex.Message);
            }
        }

        private sealed record ColumnInfo(bool IsRequired);

        /// <summary>Columns of <paramref name="table"/> in the connection's own physical order.</summary>
        private static async Task<Dictionary<string, ColumnInfo>> ReadColumnsAsync(
            NpgsqlConnection connection, string table, CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT column_name, is_nullable = 'NO' AND column_default IS NULL AS is_required
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = @table
                  AND is_generated = 'NEVER' AND identity_generation IS NULL
                ORDER BY ordinal_position
                """;

            var columns = new Dictionary<string, ColumnInfo>(StringComparer.Ordinal);
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("table", table);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns[reader.GetString(0)] = new ColumnInfo(reader.GetBoolean(1));
            }

            return columns;
        }

        /// <summary>
        /// Identifiers come from <see cref="DefaultTables"/> and from information_schema, never from a
        /// caller — but they are interpolated into SQL, so quote them anyway rather than relying on that
        /// staying true.
        /// </summary>
        private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }
}
