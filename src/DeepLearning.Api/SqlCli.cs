using DeepLearning.Infrastructure.Persistence.Sql;

namespace DeepLearning.Api
{
    /// <summary>
    /// One-off command line for the hand-authored <c>Persistence/Sql/*.sql</c> scripts:
    /// <c>dotnet run --project src/DeepLearning.Api -- sql &lt;status|baseline|apply&gt;</c>.
    /// Runs against <c>ConnectionStrings:DefaultConnection</c> and exits without starting the web host.
    ///
    ///  - <c>status</c>   list applied vs pending scripts
    ///  - <c>baseline</c> record every pending script as applied WITHOUT executing it
    ///                    (run once on a DB that is already up to date)
    ///  - <c>apply</c>    execute every pending script in manifest order, recording each
    /// </summary>
    public static class SqlCli
    {
        public static async Task<int> RunAsync(string verb, IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<SqlScriptRunner>();

            switch (verb)
            {
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
                    Console.Error.WriteLine($"unknown sql verb '{verb}' — use: status | baseline | apply");
                    return 2;
            }
        }
    }
}
