using DeepLearning.Infrastructure.Persistence;

namespace DeepLearning.UnitTests.Infrastructure
{
    /// <summary>
    /// The guard that stops "I switched to the throwaway local database" from silently meaning
    /// "I am still writing to Supabase".
    ///
    /// This is not a hypothetical: the LocalDocker launch profile was originally selected with
    /// <c>dotnet run --launchProfile</c> (the real flag is <c>--launch-profile</c>). The wrong
    /// spelling is not rejected — it is forwarded to the app as a plain argument — so dotnet run
    /// fell back to the FIRST profile in launchSettings.json, which is the Supabase one. Everything
    /// looked normal: the app booted, Hangfire installed its schema, requests worked. The only
    /// visible difference was the host in a log line nobody reads. These tests pin the behavior that
    /// now makes that combination refuse to start.
    /// </summary>
    public class DatabaseTargetResolverTests
    {
        private const string LocalDocker = "Host=localhost;Port=5433;Database=deeplearning;Username=postgres;Password=postgres";
        private const string Supabase =
            "Host=aws-0-ap-southeast-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.abc;Password=secret;SSL Mode=Require";

        [Fact]
        public void LocalDocker_profile_against_a_local_host_resolves()
        {
            var target = DatabaseTargetResolver.Resolve("LocalDocker", LocalDocker);

            Assert.Equal(DatabaseProfile.LocalDocker, target.Profile);
            Assert.True(target.Declared);
            Assert.True(target.IsLocal);
            Assert.Equal("localhost", target.Host);
            Assert.Equal(5433, target.Port);
            Assert.Equal("deeplearning", target.Database);
        }

        [Fact]
        public void Supabase_profile_against_a_remote_host_resolves()
        {
            var target = DatabaseTargetResolver.Resolve("Supabase", Supabase);

            Assert.Equal(DatabaseProfile.Supabase, target.Profile);
            Assert.True(target.Declared);
            Assert.False(target.IsLocal);
        }

        [Fact]
        public void LocalDocker_profile_pointed_at_Supabase_is_refused()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => DatabaseTargetResolver.Resolve("LocalDocker", Supabase));

            // The message has to name the host it found, or the reader cannot tell which of the two
            // halves (profile or connection string) is the one they got wrong.
            Assert.Contains("aws-0-ap-southeast-2.pooler.supabase.com", ex.Message);
            Assert.Contains("LocalDocker", ex.Message);
        }

        [Fact]
        public void Supabase_profile_pointed_at_a_local_database_is_refused()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => DatabaseTargetResolver.Resolve("Supabase", LocalDocker));

            Assert.Contains("localhost", ex.Message);
        }

        [Theory]
        [InlineData("localdocker")]
        [InlineData("LOCALDOCKER")]
        [InlineData("  LocalDocker  ")]
        public void Profile_names_are_case_and_whitespace_insensitive(string declared)
        {
            Assert.Equal(DatabaseProfile.LocalDocker, DatabaseTargetResolver.Resolve(declared, LocalDocker).Profile);
        }

        [Fact]
        public void An_unknown_profile_name_is_refused_and_lists_the_valid_ones()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => DatabaseTargetResolver.Resolve("Staging", LocalDocker));

            Assert.Contains("Staging", ex.Message);
            Assert.Contains("Supabase", ex.Message);
            Assert.Contains("LocalDocker", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void No_declared_profile_infers_from_the_host_and_says_so(string? declared)
        {
            // dotnet ef, the WebApplicationFactory test host and `dotnet run --no-launch-profile` all
            // arrive without DB_PROFILE. Refusing there would break more than it protects — but the
            // result must not claim to be a declaration.
            var local = DatabaseTargetResolver.Resolve(declared, LocalDocker);
            Assert.Equal(DatabaseProfile.LocalDocker, local.Profile);
            Assert.False(local.Declared);
            Assert.Contains("inferred", local.Describe());

            var remote = DatabaseTargetResolver.Resolve(declared, Supabase);
            Assert.Equal(DatabaseProfile.Supabase, remote.Profile);
            Assert.False(remote.Declared);
        }

        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("127.0.0.2")]
        [InlineData("::1")]
        [InlineData("host.docker.internal")]
        [InlineData("LOCALHOST")]
        public void Loopback_spellings_all_count_as_local(string host)
        {
            var target = DatabaseTargetResolver.Resolve("LocalDocker", $"Host={host};Port=5433;Database=d;Username=u;Password=p");

            Assert.True(target.IsLocal);
        }

        [Fact]
        public void A_connection_string_with_no_host_is_refused()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => DatabaseTargetResolver.Resolve("LocalDocker", "Database=deeplearning;Username=postgres"));

            Assert.Contains("no Host", ex.Message);
        }

        [Fact]
        public void Describe_never_leaks_the_password()
        {
            // Describe() goes into a log line and into GET /health/db.
            var described = DatabaseTargetResolver.Resolve("Supabase", Supabase).Describe();

            Assert.DoesNotContain("secret", described);
            Assert.DoesNotContain("Password", described, StringComparison.OrdinalIgnoreCase);
        }
    }
}
