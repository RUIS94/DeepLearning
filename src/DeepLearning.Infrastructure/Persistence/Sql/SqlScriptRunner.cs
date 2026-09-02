using System.Reflection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DeepLearning.Infrastructure.Persistence.Sql
{
    public sealed record SqlScript(string Name, string Content);

    public sealed record SqlScriptStatus(IReadOnlyList<string> Applied, IReadOnlyList<string> Pending);

    public sealed record SqlRunReport(
        IReadOnlyList<string> Ran,
        IReadOnlyList<string> Recorded,
        string? FailedScript,
        string? Error)
    {
        public bool Success => Error is null;
    }

    /// <summary>Ordered source of the hand-authored SQL scripts. Production impl = embedded resources + _manifest.txt.</summary>
    public interface ISqlScriptSource
    {
        IReadOnlyList<SqlScript> GetScripts();
    }

    /// <summary>
    /// Reads the <c>Persistence/Sql/*.sql</c> files embedded into this assembly, ordered by
    /// <c>_manifest.txt</c> (also embedded). Throws if the manifest and the embedded set disagree
    /// in either direction — that mismatch is a build-time authoring mistake, not a runtime state.
    /// </summary>
    public sealed class EmbeddedSqlScriptSource : ISqlScriptSource
    {
        private const string ResourcePrefix = "DeepLearning.Infrastructure.Persistence.Sql.";
        private static readonly Assembly Assembly = typeof(EmbeddedSqlScriptSource).Assembly;

        public IReadOnlyList<SqlScript> GetScripts()
        {
            var manifest = ReadResource("_manifest.txt")
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .ToList();

            var embedded = Assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal))
                .Select(n => n[ResourcePrefix.Length..])
                .ToHashSet(StringComparer.Ordinal);

            var notInManifest = embedded.Where(n => !manifest.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();
            if (notInManifest.Count > 0)
            {
                throw new InvalidOperationException(
                    "These embedded .sql scripts are missing from _manifest.txt: " + string.Join(", ", notInManifest));
            }

            var noResource = manifest.Where(m => !embedded.Contains(m)).ToList();
            if (noResource.Count > 0)
            {
                throw new InvalidOperationException(
                    "_manifest.txt lists scripts with no matching embedded resource: " + string.Join(", ", noResource));
            }

            var duplicates = manifest.GroupBy(m => m).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicates.Count > 0)
            {
                throw new InvalidOperationException("_manifest.txt lists these scripts more than once: " + string.Join(", ", duplicates));
            }

            return manifest.Select(name => new SqlScript(name, ReadResource(name))).ToList();
        }

        private static string ReadResource(string name)
        {
            using var stream = Assembly.GetManifestResourceStream(ResourcePrefix + name)
                ?? throw new InvalidOperationException("Embedded SQL resource not found: " + ResourcePrefix + name);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    /// <summary>
    /// Applies the hand-authored <c>Persistence/Sql/*.sql</c> scripts a target database has not seen
    /// yet, tracked in a <c>_sql_scripts</c> table (<c>name</c> PK, <c>applied_at</c>, <c>note</c>).
    ///
    /// Each script already wraps its own body in <c>BEGIN; ... COMMIT;</c>, so the runner does NOT
    /// open a transaction around the script — it executes the script text as one command (Npgsql's
    /// simple-query path, no parameters), then writes the tracking row on the same connection
    /// immediately after. If that tracking insert fails after a script committed, the run stops and
    /// reports it: the script is applied but unrecorded and would otherwise re-run.
    ///
    /// Flow for a database that is already up to date (the real Supabase DB): run
    /// <c>sql baseline</c> once to record every current script as applied WITHOUT executing it;
    /// thereafter <c>sql apply</c> runs only newly appended scripts.
    ///
    /// <para><b>Safety contract — <c>apply</c> can never re-run a historical script.</b> The DB has
    /// hand edits that live in no <c>.sql</c> file, so <c>apply</c>:</para>
    /// <list type="bullet">
    ///   <item>refuses entirely if <c>_sql_scripts</c> has no baseline (forces <c>baseline</c> first);</item>
    ///   <item>is strictly forward-only — it executes ONLY scripts positioned in the manifest AFTER
    ///   every already-recorded script, and refuses if any pending script sits before a recorded one
    ///   (a "gap" — you <c>baseline</c> or hand-apply those, <c>apply</c> won't).</item>
    /// </list>
    /// <para>Nothing invokes this on startup or during request handling — only the explicit
    /// <c>sql</c> CLI verb in <c>Program.cs</c>. A fresh-DB install is the documented manual
    /// sequence, not <c>apply</c>.</para>
    /// </summary>
    public sealed class SqlScriptRunner
    {
        public const string TrackingTable = "_sql_scripts";

        private readonly string _connectionString;
        private readonly ISqlScriptSource _source;
        private readonly ILogger<SqlScriptRunner> _logger;

        public SqlScriptRunner(string connectionString, ISqlScriptSource source, ILogger<SqlScriptRunner> logger)
        {
            _connectionString = connectionString;
            _source = source;
            _logger = logger;
        }

        public async Task<SqlScriptStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            await EnsureTrackingTableAsync(connection, cancellationToken);
            var applied = await LoadAppliedAsync(connection, cancellationToken);

            var all = _source.GetScripts().Select(s => s.Name).ToList();
            return new SqlScriptStatus(
                all.Where(applied.Contains).ToList(),
                all.Where(n => !applied.Contains(n)).ToList());
        }

        /// <param name="baselineOnly">
        /// true: record every pending script as applied (note = 'baseline') WITHOUT executing it —
        ///   position-agnostic, this is the "the DB already has all of these" declaration.
        /// false: execute pending scripts, but ONLY the manifest tail after every recorded script,
        ///   and only once a baseline exists (see the class-level safety contract).
        /// </param>
        public async Task<SqlRunReport> ApplyAsync(bool baselineOnly, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            await EnsureTrackingTableAsync(connection, cancellationToken);
            var applied = await LoadAppliedAsync(connection, cancellationToken);

            var scripts = _source.GetScripts();
            var pending = scripts.Where(s => !applied.Contains(s.Name)).ToList();
            var ran = new List<string>();
            var recorded = new List<string>();

            if (baselineOnly)
            {
                foreach (var script in pending)
                {
                    var error = await TryRecordAsync(connection, script.Name, "baseline", recorded, cancellationToken);
                    if (error is not null)
                    {
                        return new SqlRunReport(ran, recorded, script.Name, error);
                    }
                }

                return new SqlRunReport(ran, recorded, null, null);
            }

            // --- apply: baseline required + strictly forward-only (never re-runs history) ---
            var manifestIndex = scripts
                .Select((s, i) => (s.Name, i))
                .ToDictionary(x => x.Name, x => x.i, StringComparer.Ordinal);
            var recordedIndices = applied.Where(manifestIndex.ContainsKey).Select(n => manifestIndex[n]).ToList();

            if (recordedIndices.Count == 0)
            {
                return new SqlRunReport(ran, recorded, null,
                    $"refusing to apply: {TrackingTable} has no baseline. Run `sql baseline` first — it records " +
                    "every current script as already-applied WITHOUT executing any of them, so a database that " +
                    "was set up (or hand-edited) outside these files is never re-run.");
            }

            var highWater = recordedIndices.Max();
            var gaps = pending.Where(s => manifestIndex[s.Name] < highWater).Select(s => s.Name).ToList();
            if (gaps.Count > 0)
            {
                return new SqlRunReport(ran, recorded, gaps[0],
                    "refusing to apply: these scripts sit BEFORE already-applied ones in the manifest and were " +
                    $"never recorded — [{string.Join(", ", gaps)}]. `apply` never back-fills. If the database " +
                    "already has their effect, `sql baseline` them; otherwise apply them by hand.");
            }

            foreach (var script in pending.Where(s => manifestIndex[s.Name] > highWater))
            {
                try
                {
                    await using var command = new NpgsqlCommand(script.Content, connection);
                    await command.ExecuteNonQueryAsync(cancellationToken);
                    ran.Add(script.Name);
                    _logger.LogInformation("SQL script applied: {Script}", script.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SQL script failed: {Script}", script.Name);
                    return new SqlRunReport(ran, recorded, script.Name, ex.Message);
                }

                var error = await TryRecordAsync(connection, script.Name, "applied", recorded, cancellationToken);
                if (error is not null)
                {
                    return new SqlRunReport(ran, recorded, script.Name, error);
                }
            }

            return new SqlRunReport(ran, recorded, null, null);
        }

        /// <summary>Records <paramref name="name"/> and appends it to <paramref name="recorded"/>; returns null on success or an error message.</summary>
        private async Task<string?> TryRecordAsync(
            NpgsqlConnection connection, string name, string note, List<string> recorded, CancellationToken cancellationToken)
        {
            try
            {
                await RecordAsync(connection, name, note, cancellationToken);
                recorded.Add(name);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SQL script {Script} {State} but its {Table} row was not written", name, note, TrackingTable);
                return $"tracking-row insert failed ({ex.Message}); add '{name}' to {TrackingTable} by hand or it will run again";
            }
        }

        private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
        {
            var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        private static async Task EnsureTrackingTableAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
        {
            var ddl = $"""
                CREATE TABLE IF NOT EXISTS {TrackingTable} (
                    name       text PRIMARY KEY,
                    applied_at timestamptz NOT NULL DEFAULT now(),
                    note       text NOT NULL DEFAULT 'applied'
                )
                """;
            await using var command = new NpgsqlCommand(ddl, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task<HashSet<string>> LoadAppliedAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            await using var command = new NpgsqlCommand($"SELECT name FROM {TrackingTable}", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                names.Add(reader.GetString(0));
            }

            return names;
        }

        private static async Task RecordAsync(NpgsqlConnection connection, string name, string note, CancellationToken cancellationToken)
        {
            await using var command = new NpgsqlCommand(
                $"INSERT INTO {TrackingTable} (name, note) VALUES (@name, @note) ON CONFLICT (name) DO NOTHING",
                connection);
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("note", note);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
