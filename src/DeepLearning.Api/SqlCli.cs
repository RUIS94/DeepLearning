using DeepLearning.Infrastructure.Persistence;
using DeepLearning.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Api
{
    /// <summary>
    /// One-off command line for the hand-authored <c>Persistence/Sql/*.sql</c> scripts:
    /// <c>dotnet run --project src/DeepLearning.Api -- sql &lt;status|baseline|apply|bootstrap&gt;</c>.
    /// Runs against <c>ConnectionStrings:DefaultConnection</c> and exits without starting the web host.
    ///
    ///  - <c>status</c>    list applied vs pending scripts
    ///  - <c>baseline</c>  record every pending script as applied WITHOUT executing it
    ///                     (run once on a DB that is already up to date)
    ///  - <c>apply</c>     execute every pending script in manifest order, recording each
    ///  - <c>bootstrap</c> fresh LOCAL database only: EF-migrate, then run the whole manifest minus
    ///                     <c>_bootstrap_skip.txt</c>, so a throwaway container comes up with the same
    ///                     reference/seed data (exam_types, assessment_dimensions, error_taxonomies,
    ///                     generation_policy, llm_provider_*, prompt_templates, weak_point_catalog)
    ///                     as the shared database
    /// </summary>
    public static class SqlCli
    {
        public static async Task<int> RunAsync(string verb, IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<SqlScriptRunner>();

            switch (verb)
            {
                case "bootstrap":
                {
                    // Structural isolation (AGENTS.md #4): bootstrap replays seed scripts, so it must be
                    // impossible to aim at the shared database — not merely discouraged in a comment.
                    var target = scope.ServiceProvider.GetRequiredService<DatabaseTarget>();
                    if (!target.IsLocal)
                    {
                        Console.Error.WriteLine(
                            $"refusing to bootstrap {target.Describe()} — `sql bootstrap` only ever runs against a " +
                            "database on this machine (the docker-compose Postgres). Nothing was executed.");
                        return 1;
                    }

                    Console.WriteLine($"bootstrapping {target.Describe()}");

                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await context.Database.MigrateAsync();
                    Console.WriteLine("  EF migrations applied");

                    var bootstrapReport = await runner.BootstrapAsync();
                    foreach (var name in bootstrapReport.Recorded)
                    {
                        var executed = bootstrapReport.Ran.Contains(name);
                        Console.WriteLine($"  {(executed ? "ran" : "skipped")}: {name}");
                    }

                    if (!bootstrapReport.Success)
                    {
                        var at = bootstrapReport.FailedScript is null ? string.Empty : $" at {bootstrapReport.FailedScript}";
                        Console.Error.WriteLine($"FAILED{at}: {bootstrapReport.Error}");
                        return 1;
                    }

                    Console.WriteLine($"done — {bootstrapReport.Ran.Count} script(s) run, " +
                        $"{bootstrapReport.Recorded.Count - bootstrapReport.Ran.Count} skipped as EF-owned");
                    return 0;
                }

                case "status":
                {
                    var status = await runner.GetStatusAsync();
                    Console.WriteLine($"applied: {status.Applied.Count}");
                    foreach (var name in status.Applied)
                    {
                        Console.WriteLine($"  [x] {name}");
                    }

                    Console.WriteLine($"pending: {status.Pending.Count}");
                    foreach (var name in status.Pending)
                    {
                        Console.WriteLine($"  [ ] {name}");
                    }

                    return 0;
                }

                case "baseline":
                case "apply":
                {
                    var baselineOnly = verb == "baseline";
                    var report = await runner.ApplyAsync(baselineOnly);

                    var verbed = baselineOnly ? "baselined" : "applied";
                    foreach (var name in report.Recorded)
                    {
                        Console.WriteLine($"  {verbed}: {name}");
                    }

                    if (!report.Success)
                    {
                        var at = report.FailedScript is null ? string.Empty : $" at {report.FailedScript}";
                        Console.Error.WriteLine($"FAILED{at}: {report.Error}");
                        if (report.Recorded.Count > 0)
                        {
                            Console.Error.WriteLine($"({report.Recorded.Count} recorded before the failure)");
                        }

                        return 1;
                    }

                    Console.WriteLine(report.Recorded.Count == 0
                        ? "nothing pending — database is up to date"
                        : $"done — {report.Recorded.Count} script(s) {verbed}");
                    return 0;
                }

                default:
                    Console.Error.WriteLine($"unknown sql verb '{verb}' — use: status | baseline | apply | bootstrap");
                    return 2;
            }
        }
    }
}
