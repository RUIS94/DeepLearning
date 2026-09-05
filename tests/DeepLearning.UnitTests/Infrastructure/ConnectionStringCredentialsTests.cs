using DeepLearning.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;

namespace DeepLearning.UnitTests.Infrastructure
{
    /// <summary>
    /// The Supabase database login lives in User Secrets next to the LLM API keys, while the rest of
    /// the connection string stays in appsettings.Development.json. These pin the merge rules — in
    /// particular that a connection string which already names credentials is never overridden, since
    /// that is how the LocalDocker profile keeps pointing at postgres/postgres.
    /// </summary>
    public class ConnectionStringCredentialsTests
    {
        private const string WithoutLogin = "Host=db.example.com;Port=5432;Database=postgres;SSL Mode=Require";
        private const string LocalWithLogin = "Host=localhost;Port=5433;Database=deeplearning;Username=postgres;Password=postgres";

        private static IConfiguration Config(string? username, string? password)
            => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ConnectionStringCredentials.UsernameKey] = username,
                [ConnectionStringCredentials.PasswordKey] = password,
            }).Build();

        [Fact]
        public void Credentials_are_filled_in_from_configuration()
        {
            var merged = ConnectionStringCredentials.Apply(WithoutLogin, Config("postgres.abc", "s3cret"));

            var builder = new Npgsql.NpgsqlConnectionStringBuilder(merged);
            Assert.Equal("postgres.abc", builder.Username);
            Assert.Equal("s3cret", builder.Password);
            // The non-secret half survives untouched.
            Assert.Equal("db.example.com", builder.Host);
            Assert.Equal("postgres", builder.Database);
            Assert.Equal(Npgsql.SslMode.Require, builder.SslMode);
        }

        [Fact]
        public void A_connection_string_that_already_has_credentials_is_left_alone()
        {
            // The LocalDocker launch profile pins its own login; a Supabase secret must never displace
            // it, or "run against the throwaway container" would start failing authentication.
            var merged = ConnectionStringCredentials.Apply(LocalWithLogin, Config("postgres.abc", "s3cret"));

            var builder = new Npgsql.NpgsqlConnectionStringBuilder(merged);
            Assert.Equal("postgres", builder.Username);
            Assert.Equal("postgres", builder.Password);
        }

        [Fact]
        public void A_missing_username_fails_with_the_command_that_fixes_it()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => ConnectionStringCredentials.Apply(WithoutLogin, Config(null, null)));

            Assert.Contains("dotnet user-secrets set", ex.Message);
            Assert.Contains(ConnectionStringCredentials.UsernameKey, ex.Message);
        }

        [Fact]
        public void A_password_only_secret_still_fails_on_the_username()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => ConnectionStringCredentials.Apply(WithoutLogin, Config(null, "s3cret")));

            Assert.Contains("has no Username", ex.Message);
        }

        [Fact]
        public void The_merge_runs_before_the_profile_cross_check_so_a_composed_string_still_resolves()
        {
            // Order matters: DatabaseTargetResolver parses the connection string, so a string that is
            // only complete after the merge has to be merged first.
            var merged = ConnectionStringCredentials.Apply(WithoutLogin, Config("postgres.abc", "s3cret"));

            var target = DatabaseTargetResolver.Resolve("Supabase", merged);

            Assert.Equal(DatabaseProfile.Supabase, target.Profile);
            Assert.False(target.IsLocal);
            Assert.DoesNotContain("s3cret", target.Describe());
        }
    }
}
