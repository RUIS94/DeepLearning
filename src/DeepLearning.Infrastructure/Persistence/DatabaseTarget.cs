using Npgsql;

namespace DeepLearning.Infrastructure.Persistence
{
    /// <summary>Which database this process is talking to — declared by the caller, not guessed.</summary>
    public enum DatabaseProfile
    {
        /// <summary>The hosted Supabase Postgres — shared, real data, never disposable.</summary>
        Supabase,

        /// <summary>The docker-compose Postgres on this machine — throwaway test data.</summary>
        LocalDocker,
    }

    /// <summary>
    /// The resolved answer to "which database am I about to write to". Registered as a singleton by
    /// AddInfrastructure and reported by <c>GET /health/db</c>, so the answer is visible in logs and
    /// over HTTP instead of being inferred from a connection string nobody reads.
    /// </summary>
    /// <param name="Profile">The declared (or, when <c>DB_PROFILE</c> is unset, inferred) profile.</param>
    /// <param name="Declared">False when the profile was inferred from the host rather than declared.</param>
    public sealed record DatabaseTarget(DatabaseProfile Profile, string Host, int Port, string Database, bool Declared)
    {
        public bool IsLocal => DatabaseTargetResolver.IsLocalHost(Host);

        /// <summary>Host/port/database only — never the password, this ends up in logs and an HTTP response.</summary>
        public string Describe() => $"{Profile}{(Declared ? "" : " (inferred)")} → {Host}:{Port}/{Database}";
    }

    /// <summary>
    /// Cross-checks the declared <c>DB_PROFILE</c> against the connection string actually configured,
    /// and hard-fails when they disagree.
    ///
    /// <para>This exists because the failure it prevents is silent and expensive: a launch profile that
    /// says <c>LocalDocker</c> while <c>ConnectionStrings:DefaultConnection</c> still resolves to Supabase
    /// looks completely normal at startup — the app boots, Hangfire installs its schema, tests write rows —
    /// and the "throwaway" test data lands in the real database. The reverse (thinking you're on Supabase
    /// while actually on an empty local container) is equally confusing: every query returns nothing.
    /// Per AGENTS.md #1, this validates against a known-good set and hard-fails rather than guessing.</para>
    ///
    /// <para>An unset/blank <c>DB_PROFILE</c> is allowed and infers the profile from the host — <c>dotnet ef</c>,
    /// the test host and a bare <c>dotnet run --no-launch-profile</c> all arrive without one, and refusing to
    /// start there would break more than it protects. The declared-vs-inferred distinction is surfaced in
    /// <see cref="DatabaseTarget.Describe"/> so a log line never claims more certainty than it has.</para>
    /// </summary>
    public static class DatabaseTargetResolver
    {
        /// <summary>Config key (also the environment-variable name) carrying the declared profile.</summary>
        public const string ProfileConfigKey = "DB_PROFILE";

        private static readonly string[] LocalHosts =
        [
            "localhost",
            "127.0.0.1",
            "::1",
            "[::1]",
            "host.docker.internal",
        ];

        public static bool IsLocalHost(string host)
            => LocalHosts.Contains(host, StringComparer.OrdinalIgnoreCase)
                || host.StartsWith("127.", StringComparison.Ordinal);

        /// <param name="declaredProfile">The <c>DB_PROFILE</c> value; null/blank means "infer from the host".</param>
        /// <param name="connectionString">The <c>DefaultConnection</c> string this process would actually use.</param>
        /// <exception cref="InvalidOperationException">
        /// The profile name is not one of <see cref="DatabaseProfile"/>, the connection string is unparseable
        /// or has no host, or the declared profile contradicts the host.
        /// </exception>
        public static DatabaseTarget Resolve(string? declaredProfile, string connectionString)
        {
            NpgsqlConnectionStringBuilder builder;
            try
            {
                builder = new NpgsqlConnectionStringBuilder(connectionString);
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                throw new InvalidOperationException(
                    $"ConnectionStrings:DefaultConnection is not a valid Npgsql connection string: {ex.Message}", ex);
            }

            var host = builder.Host;
            if (string.IsNullOrWhiteSpace(host))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection has no Host — cannot tell which database this would write to.");
            }

            var port = builder.Port;
            var database = builder.Database ?? string.Empty;
            var isLocal = IsLocalHost(host);

            if (string.IsNullOrWhiteSpace(declaredProfile))
            {
                var inferred = isLocal ? DatabaseProfile.LocalDocker : DatabaseProfile.Supabase;
                return new DatabaseTarget(inferred, host, port, database, Declared: false);
            }

            if (!Enum.TryParse<DatabaseProfile>(declaredProfile.Trim(), ignoreCase: true, out var profile))
            {
                var valid = string.Join(", ", Enum.GetNames<DatabaseProfile>());
                throw new InvalidOperationException(
                    $"{ProfileConfigKey}='{declaredProfile}' is not a known database profile. Valid values: {valid} " +
                    "(or leave it unset to infer from the connection string's host).");
            }

            if (profile == DatabaseProfile.LocalDocker && !isLocal)
            {
                throw new InvalidOperationException(
                    $"{ProfileConfigKey}=LocalDocker but ConnectionStrings:DefaultConnection points at '{host}', which is " +
                    "not this machine. Refusing to start: this is how throwaway test data ends up in the real Supabase " +
                    "database. Set ConnectionStrings__DefaultConnection to the docker-compose Postgres " +
                    "(Host=localhost;Port=5433;Database=deeplearning;Username=postgres;Password=postgres), or drop " +
                    $"{ProfileConfigKey} if you really meant to use the remote database.");
            }

            if (profile == DatabaseProfile.Supabase && isLocal)
            {
                throw new InvalidOperationException(
                    $"{ProfileConfigKey}=Supabase but ConnectionStrings:DefaultConnection points at '{host}' (this machine). " +
                    "Refusing to start: you would be reading an empty local database while believing it is the shared one. " +
                    $"Use the LocalDocker launch profile, or unset {ProfileConfigKey}.");
            }

            return new DatabaseTarget(profile, host, port, database, Declared: true);
        }
    }
}
