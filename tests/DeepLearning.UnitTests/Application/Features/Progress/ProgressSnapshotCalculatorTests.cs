using DeepLearning.Application.Features.Progress;
using DeepLearning.Domain.Entities;

namespace DeepLearning.UnitTests.Application.Features.Progress
{
    /// <summary>
    /// Pure logic extracted from UpdateProgressOnGraded (Step 6) so Step 9's weekly/backfill
    /// snapshot generation can reuse it — design doc §11.2's own "单元测试(统计计算逻辑)" ask for
    /// Step 9. No DB involved; GradingResult.Dimension is populated by hand the same way the
    /// repository's real Include(...) call would.
    /// </summary>
    public class ProgressSnapshotCalculatorTests
    {
        private static GradingResult Result(string dimensionKey, int band, bool passBool) => new()
        {
            Id = Guid.NewGuid(),
            SubmissionId = Guid.NewGuid(),
            DimensionId = Guid.NewGuid(),
            RubricVersion = "2024-02",
            Band = band,
            PassBool = passBool,
            Rationale = "test",
            CreatedAt = DateTimeOffset.UtcNow,
            Dimension = new AssessmentDimension
            {
                Id = Guid.NewGuid(),
                DimensionKey = dimensionKey,
                DimensionName = dimensionKey,
                RubricVersion = "2024-02",
                LevelDescriptions = "{}",
                EffectiveFrom = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
            },
        };

        [Fact]
        public void Empty_input_produces_all_null_measures()
        {
            var aggregate = ProgressSnapshotCalculator.Compute([]);

            Assert.Null(aggregate.AvgBandMeaningTransfer);
            Assert.Null(aggregate.AvgBandTextualNorms);
            Assert.Null(aggregate.AvgBandLanguageProficiency);
            Assert.Null(aggregate.PassRate);
        }

        [Fact]
        public void Averages_each_dimension_independently_and_computes_pass_rate_by_submission()
        {
            // submissionA's own SubmissionId is shared by its two dimension rows below (the same
            // grading call produces one GradingResult row per assessment_dimensions row) —
            // PassBool is per-row, but "did the submission pass" (used for pass_rate) is
            // per-submission: only counts if every one of its rows passed.
            var submissionAId = Guid.NewGuid();
            var submissionBId = Guid.NewGuid();

            var meaningA = Result("meaning_transfer", band: 2, passBool: true);
            meaningA.SubmissionId = submissionAId;
            var textualA = Result("textual_norms", band: 4, passBool: false);
            textualA.SubmissionId = submissionAId;

            var meaningB = Result("meaning_transfer", band: 4, passBool: true);
            meaningB.SubmissionId = submissionBId;

            var aggregate = ProgressSnapshotCalculator.Compute([meaningA, textualA, meaningB]);

            // (2 + 4) / 2 = 3.0
            Assert.Equal(3.0m, aggregate.AvgBandMeaningTransfer);
            Assert.Equal(4.0m, aggregate.AvgBandTextualNorms);
            Assert.Null(aggregate.AvgBandLanguageProficiency);
            // submissionA has one failing row (textualA) so it doesn't count as passed overall;
            // submissionB's only row passed -> 1 of 2 submissions passed = 50%.
            Assert.Equal(50.00m, aggregate.PassRate);
        }
    }
}
