using Microsoft.Extensions.Configuration;
using Npgsql;

namespace DeepLearning.Infrastructure.Persistence
{
    /// <summary>
    /// Fills a connection string's <c>Username</c>/<c>Password</c> from configuration when it does not
    /// carry them itself.
    ///
    /// <para>The point is to keep the Supabase database credentials in the same place as the LLM API
    /// keys — .NET User Secrets (<c>secrets.json</c>, outside the repo entirely) — while the part of the
    /// connection string that is not a secret (host, port, database, SSL mode) stays readable and
    /// diffable in appsettings.Development.json. One pair of credentials covers every Supabase
    /// connection string, because they are all the same database.</para>
    ///
    /// <para>A connection string that already names a username/password keeps them untouched: the
    /// LocalDocker launch profile pins <c>postgres/postgres</c> inline, and CI or a one-off
    /// <c>--source</c> may pass a complete string. So this is a fallback, never an override — a
    /// caller that spelled out credentials always means them.</para>
    /// </summary>
    public static class ConnectionStringCredentials
    {
        public const string UsernameKey = "Supabase:Username";
        public const string PasswordKey = "Supabase:Password";

        public static string Apply(string connectionString, IConfiguration configuration)
            => Apply(connectionString, configuration[UsernameKey], configuration[PasswordKey]);

        /// <exception cref="InvalidOperationException">
        /// Neither the connection string nor configuration supplies a username — failing here, naming the
        /// exact command that fixes it, beats an Npgsql authentication error further downstream.
        /// </exception>
        public static string Apply(string connectionString, string? username, string? password)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);

            if (string.IsNullOrWhiteSpace(builder.Username) && !string.IsNullOrWhiteSpace(username))
            {
                builder.Username = username;
            }

            if (string.IsNullOrWhiteSpace(builder.Password) && !string.IsNullOrWhiteSpace(password))
            {
                builder.Password = password;
            }

            if (string.IsNullOrWhiteSpace(builder.Username))
            {
                throw new InvalidOperationException(
                    $"the connection string for '{builder.Host}' has no Username, and {UsernameKey} is not configured. " +
                    "The Supabase database credentials live in User Secrets alongside the LLM API keys — set them with:\n" +
                    $"  dotnet user-secrets set \"{UsernameKey}\" \"<...>\" --project src/DeepLearning.Api\n" +
                    $"  dotnet user-secrets set \"{PasswordKey}\" \"<...>\" --project src/DeepLearning.Api");
            }

            return builder.ConnectionString;
        }
    }
}
