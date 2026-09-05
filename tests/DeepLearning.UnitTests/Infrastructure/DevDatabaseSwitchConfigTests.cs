using System.Text.Json;
using System.Text.RegularExpressions;
using DeepLearning.Infrastructure.Persistence;

namespace DeepLearning.UnitTests.Infrastructure
{
    /// <summary>
    /// The dev-time "which database" switch is spread across three files that must agree —
    /// launchSettings.json (the profiles), docker-compose.yml (the container those profiles point
    /// at) and dev.ps1 (what actually invokes them). Nothing compiles them together, so a typo in
    /// any one of them is only discovered by noticing the wrong data, which is exactly what
    /// happened: dev.ps1 passed <c>--launchProfile</c> instead of <c>--launch-profile</c>, dotnet
    /// run forwarded the unknown flag to the app as a plain argument and silently fell back to the
    /// FIRST profile — the Supabase one — so "start against the local throwaway DB" ran against
    /// production instead, with no error anywhere.
    ///
    /// These tests read the real files from the repo. They are cheap, need no container, and cover
    /// the failure mode that unit-testing C# cannot reach.
    /// </summary>
    public class DevDatabaseSwitchConfigTests
    {
        private static readonly string RepoRoot = FindRepoRoot();

        private static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeepLearning.slnx")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName
                ?? throw new InvalidOperationException("Could not locate the repo root (no DeepLearning.slnx above the test assembly).");
        }

        private static JsonElement LaunchProfiles()
        {
            var json = File.ReadAllText(Path.Combine(RepoRoot, "src", "DeepLearning.Api", "Properties", "launchSettings.json"));
            return JsonDocument.Parse(json).RootElement.GetProperty("profiles");
        }

        private static string ComposeFile() => File.ReadAllText(Path.Combine(RepoRoot, "docker-compose.yml"));

        private static string DevScript() => File.ReadAllText(Path.Combine(RepoRoot, "dev.ps1"));

        private static string? EnvVar(JsonElement profile, string name)
            => profile.TryGetProperty("environmentVariables", out var env) && env.TryGetProperty(name, out var value)
                ? value.GetString()
                : null;

        [Fact]
        public void Every_launch_profile_declares_which_database_it_means()
        {
            foreach (var profile in LaunchProfiles().EnumerateObject())
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(EnvVar(profile.Value, "DB_PROFILE")),
                    $"launch profile '{profile.Name}' has no DB_PROFILE — nothing would cross-check its connection string.");
            }
        }

        [Fact]
        public void Each_launch_profile_passes_its_own_cross_check()
        {
            // A profile that declares LocalDocker while carrying (or inheriting) a remote connection
            // string is the whole class of bug this guard exists for — assert the shipped profiles
            // are on the right side of it.
            foreach (var profile in LaunchProfiles().EnumerateObject())
            {
                var declared = EnvVar(profile.Value, "DB_PROFILE");
                var connectionString = EnvVar(profile.Value, "ConnectionStrings__DefaultConnection");

                if (declared == "LocalDocker")
                {
                    Assert.NotNull(connectionString);
                    var target = DatabaseTargetResolver.Resolve(declared, connectionString);
                    Assert.True(target.IsLocal);
                }
                else
                {
                    // The Supabase profiles must NOT pin a connection string: the real one lives in
                    // the gitignored appsettings.Development.json, and an env var here would win
                    // over it (env vars outrank JSON in the default configuration order).
                    Assert.Null(connectionString);
                }
            }
        }

        [Fact]
        public void No_launch_profile_blanks_the_Supabase_project_url()
        {
            // Program.cs treats an empty Supabase:ProjectUrl as "no token will ever validate", so
            // blanking it in the LocalDocker profiles made every authenticated request 401 — the
            // frontend logs in fine (it talks to Supabase Auth directly) and then nothing works.
            // Auth and data storage are independent: which Postgres holds the rows has nothing to
            // do with who issues the JWT.
            foreach (var profile in LaunchProfiles().EnumerateObject())
            {
                var url = EnvVar(profile.Value, "Supabase__ProjectUrl");
                Assert.False(
                    url is not null && string.IsNullOrWhiteSpace(url),
                    $"launch profile '{profile.Name}' blanks Supabase__ProjectUrl, which disables JWT validation entirely.");
            }
        }

        [Fact]
        public void The_LocalDocker_profiles_point_at_the_database_docker_compose_actually_publishes()
        {
            var compose = ComposeFile();

            var publishedPort = Regex.Match(compose, @"""(?<host>\d+):5432""").Groups["host"].Value;
            var databaseName = Regex.Match(compose, @"POSTGRES_DB:\s*(?<db>\S+)").Groups["db"].Value;
            var user = Regex.Match(compose, @"POSTGRES_USER:\s*(?<u>\S+)").Groups["u"].Value;
            Assert.NotEmpty(publishedPort);
            Assert.NotEmpty(databaseName);

            foreach (var profile in LaunchProfiles().EnumerateObject())
            {
                if (EnvVar(profile.Value, "DB_PROFILE") != "LocalDocker")
                {
                    continue;
                }

                var target = DatabaseTargetResolver.Resolve(
                    "LocalDocker", EnvVar(profile.Value, "ConnectionStrings__DefaultConnection")!);

                Assert.Equal(int.Parse(publishedPort), target.Port);
                Assert.Equal(databaseName, target.Database);
                Assert.Contains($"Username={user}", EnvVar(profile.Value, "ConnectionStrings__DefaultConnection")!);
            }
        }

        /// <summary>
        /// appsettings.Development.json is committed, which is only safe while it stays free of
        /// credentials. Every secret belongs in .NET User Secrets (secrets.json, outside the repo) —
        /// the Supabase database login under <c>Supabase:Username</c>/<c>Supabase:Password</c>, the LLM
        /// keys under <c>Llm:*:ApiKey</c>. This test is what lets that file be in git at all: put a
        /// credential back and the build fails before the commit lands.
        /// </summary>
        [Fact]
        public void The_committed_dev_settings_file_contains_no_credentials()
        {
            var path = Path.Combine(RepoRoot, "src", "DeepLearning.Api", "appsettings.Development.json");
            Assert.True(File.Exists(path), $"{path} is missing — it is committed config, not a local-only file.");

            var root = JsonDocument.Parse(File.ReadAllText(path)).RootElement;

            foreach (var entry in root.GetProperty("ConnectionStrings").EnumerateObject())
            {
                if (entry.Name.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var value = entry.Value.GetString() ?? string.Empty;
                Assert.DoesNotContain("Password=", value, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Username=", value, StringComparison.OrdinalIgnoreCase);
                // Should still be a usable half — host and database are the non-secret part.
                Assert.Contains("Host=", value, StringComparison.OrdinalIgnoreCase);
            }

            // Any *Key / *Secret / *Token leaf must be absent or empty, whatever nesting it sits at.
            var offenders = new List<string>();
            CollectSecretLikeValues(root, string.Empty, offenders);
            Assert.True(offenders.Count == 0,
                "appsettings.Development.json carries values that look like credentials: " + string.Join(", ", offenders) +
                ". Move them to User Secrets: dotnet user-secrets set \"<key>\" \"<value>\" --project src/DeepLearning.Api");
        }

        private static void CollectSecretLikeValues(JsonElement element, string path, List<string> offenders)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    CollectSecretLikeValues(property.Value, path.Length == 0 ? property.Name : $"{path}:{property.Name}", offenders);
                }

                return;
            }

            if (element.ValueKind != JsonValueKind.String)
            {
                return;
            }

            var looksSecret = path.EndsWith("ApiKey", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("Secret", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("Token", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("Password", StringComparison.OrdinalIgnoreCase);

            if (looksSecret && !string.IsNullOrWhiteSpace(element.GetString()))
            {
                offenders.Add(path);
            }
        }

        [Fact]
        public void Dev_script_uses_the_real_dotnet_run_flag_name()
        {
            // Comment lines are excluded on purpose: dev.ps1's own header explains the wrong
            // spelling, and a test that flagged the warning about the bug as the bug would be
            // impossible to satisfy.
            var code = string.Join(
                '\n',
                DevScript().Split('\n').Where(line => !line.TrimStart().StartsWith('#')));

            // `--launchProfile` is not rejected by dotnet run — it is forwarded to the application
            // as an argument, and the launch profile silently defaults to the first one listed.
            var invocations = Regex.Matches(code, @"--launch[A-Za-z-]*");
            Assert.NotEmpty(invocations);
            Assert.All(invocations, m => Assert.Equal("--launch-profile", m.Value));
        }

        [Fact]
        public void Every_profile_name_dev_script_can_select_exists_in_launchSettings()
        {
            var known = LaunchProfiles().EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

            // The names dev.ps1 defaults to, written as `"http (LocalDocker)"` / `"http (Supabase)"`.
            var referenced = Regex.Matches(DevScript(), @"""(?<name>https?\s*\([^""]+\))""")
                .Select(m => m.Groups["name"].Value)
                .ToList();

            Assert.NotEmpty(referenced);
            Assert.All(referenced, name => Assert.Contains(name, known));
        }
    }
}
