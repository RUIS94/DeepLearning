using DeepLearning.Infrastructure.Persistence;
using DeepLearning.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Api
{
    /// <summary>
    /// Database-level one-off commands, as opposed to <see cref="SqlCli"/>'s script-level ones:
    /// <c>dotnet run --project src/DeepLearning.Api -- db pull-reference [--source "&lt;conn&gt;"] [--dry-run]</c>.
    ///
    /// <para><c>pull-reference</c> copies the reference/config tables from the shared Supabase database into
    /// the local one this process is configured with, replacing what is there. Use it instead of
    /// <c>sql bootstrap</c> whenever Supabase is reachable: the seed scripts reproduce what was seeded, not
    /// the hand edits the shared database has accumulated since (see <see cref="ReferenceDataSync"/>).</para>
    ///
    /// <para>The source connection string comes from <c>--source</c>, or from
    /// <c>ConnectionStrings:ReferenceSource</c> in appsettings.Development.json. It is never the process's
    /// own <c>DefaultConnection</c>: that one is the destination, and the destination is always local.</para>
    /// </summary>
    public static class DbCli
    {
        public const string SourceConnectionStringName = "ReferenceSource";

        public static async Task<int> RunAsync(string verb, string[] rest, IServiceProvider services)
        {
            using var scope = services.CreateScope();

            switch (verb)
            {
                case "pull-reference":
                {
                    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                    var target = scope.ServiceProvider.GetRequiredService<DatabaseTarget>();

                    var source = ArgumentValue(rest, "--source")
                        ?? configuration.GetConnectionString(SourceConnectionStringName);
                    if (string.IsNullOrWhiteSpace(source))
                    {
                        Console.Error.WriteLine(
                            "no source database. Pass --source \"Host=...;Database=...\", or add a " +
                            $"ConnectionStrings:{SourceConnectionStringName} entry to appsettings.Development.json. " +
                            "It must be the shared Supabase database — this command only ever reads from it " +
                            "(the source session is opened read-only).");
                        return 2;
                    }

                    // Same credential merge as DefaultConnection: ReferenceSource in
                    // appsettings.Development.json names the host and database, not the login.
                    source = ConnectionStringCredentials.Apply(source, configuration);

                    var tables = ReferenceDataSync.DefaultTables;

                    Console.WriteLine($"destination: {target.Describe()}");
                    Console.WriteLine($"tables:      {string.Join(", ", tables)}");
                    Console.WriteLine(
                        "each of these is TRUNCATE ... CASCADE'd before it is refilled — local rows in the " +
                        "business tables that reference them (submissions, weak_points, ...) go with them.");

                    if (rest.Contains("--dry-run"))
                    {
                        Console.WriteLine("--dry-run: nothing was read or written.");
                        return 0;
                    }

                    // Idempotent, and makes `pull-reference` usable on a container that has only just been
                    // created — the schema has to exist before anything can be copied into it.
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await context.Database.MigrateAsync();

                    var sync = scope.ServiceProvider.GetRequiredService<ReferenceDataSync>();
                    var report = await sync.CopyAsync(source, tables);

                    foreach (var table in report.Copied)
                    {
                        Console.WriteLine($"  {table.Table}: {table.Rows} row(s)");
                        if (table.IgnoredSourceColumns.Count > 0)
                        {
                            // Not fatal (the local schema can live without them) but never silent: a column
                            // that exists upstream and not here is schema drift worth knowing about.
                            Console.WriteLine(
                                $"    note: source columns not present locally, skipped — {string.Join(", ", table.IgnoredSourceColumns)}");
                        }
                    }

                    if (!report.Success)
                    {
                        var at = report.FailedTable is null ? string.Empty : $" at {report.FailedTable}";
                        Console.Error.WriteLine($"FAILED{at}: {report.Error}");
                        Console.Error.WriteLine("(the copy runs in one transaction — the local database is unchanged)");
                        return 1;
                    }

                    // A container that was only ever pulled into has no _sql_scripts history, so a later
                    // `sql apply` would refuse for lack of a baseline. But the copied rows ARE the result
                    // of every script having run upstream, so recording them as baselined is the accurate
                    // statement — same declaration `sql baseline` makes about the shared database itself.
                    // No-op on a container that was bootstrapped first.
                    var runner = scope.ServiceProvider.GetRequiredService<SqlScriptRunner>();
                    var baseline = await runner.ApplyAsync(baselineOnly: true);
                    if (!baseline.Success)
                    {
                        Console.Error.WriteLine($"copied, but recording the script baseline failed: {baseline.Error}");
                        return 1;
                    }

                    if (baseline.Recorded.Count > 0)
                    {
                        Console.WriteLine($"  baselined {baseline.Recorded.Count} SQL script(s) — `sql apply` can move this database forward from here");
                    }

                    Console.WriteLine($"done — {report.Copied.Sum(t => t.Rows)} row(s) across {report.Copied.Count} table(s)");
                    return 0;
                }

                default:
                    Console.Error.WriteLine($"unknown db verb '{verb}' — use: pull-reference");
                    return 2;
            }
        }

        private static string? ArgumentValue(string[] args, string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
