using DeepLearning.Infrastructure.BackgroundJobs;

namespace DeepLearning.Api
{
    /// <summary>
    /// On-demand trigger for <see cref="PromptRegressionTestJob"/> (design doc §7 "Prompt回归测试"):
    /// <c>dotnet run --project src/DeepLearning.Api -- prompt-regression &lt;examTypeId&gt; [sampleSize] [--apply]</c>.
    ///
    /// <para>Dry run by default — it only reports the current Band distribution over the recent
    /// graded submissions. <c>--apply</c> actually re-grades them (real LLM cost) and diffs the
    /// distribution. The job is a skeleton; see its doc comment for what a real CI gate still needs.</para>
    /// </summary>
    public static class PromptRegressionCli
    {
        public static async Task<int> RunAsync(string[] rest, IServiceProvider services)
        {
            if (rest.Length == 0 || !Guid.TryParse(rest[0], out var examTypeId))
            {
                Console.Error.WriteLine(
                    "usage: prompt-regression <examTypeId> [sampleSize] [--apply]\n" +
                    "  examTypeId  required GUID\n" +
                    "  sampleSize  optional, default " + PromptRegressionTestJob.DefaultSampleSize + "\n" +
                    "  --apply     re-grade the sample with the current prompt (costs real LLM calls); omitted = dry run");
                return 2;
            }

            var sampleSize = PromptRegressionTestJob.DefaultSampleSize;
            if (rest.Length > 1 && int.TryParse(rest[1], out var parsed) && parsed > 0)
            {
                sampleSize = parsed;
            }

            var dryRun = !rest.Contains("--apply");

            using var scope = services.CreateScope();
            var job = scope.ServiceProvider.GetRequiredService<PromptRegressionTestJob>();

            Console.WriteLine($"prompt-regression: examType={examTypeId} sampleSize={sampleSize} dryRun={dryRun}");
            var report = await job.RunAsync(examTypeId, sampleSize, dryRun);

            Console.WriteLine($"sampled:  {report.SampledCount} submission(s)");
            Console.WriteLine("before:   " + Format(report.Before));
            if (report.After is not null)
            {
                Console.WriteLine("after:    " + Format(report.After));
                Console.WriteLine($"drift:    {report.DriftDetected}");
                return report.DriftDetected ? 1 : 0;
            }

            Console.WriteLine("(dry run — pass --apply to re-grade and diff)");
            return 0;
        }

        private static string Format(IReadOnlyDictionary<string, int[]> histogram)
            => histogram.Count == 0
                ? "(no graded results)"
                : string.Join("; ", histogram.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}[{string.Join(",", kv.Value)}]"));
    }
}
