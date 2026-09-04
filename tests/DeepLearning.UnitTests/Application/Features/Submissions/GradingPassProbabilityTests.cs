using DeepLearning.Application.Features.Submissions.Commands.GradeSubmission;
using DeepLearning.Domain.Entities;

namespace DeepLearning.UnitTests.Application.Features.Submissions
{
    /// <summary>
    /// Pass probability used to come from the AI and was the single most-complained-about number
    /// in the grading output: it stamped the same value into every dimension (observed 0.40, then
    /// 0.55 once a gap table was added — a table keyed only on band-minus-threshold cannot tell
    /// two dimensions at the same gap apart), and the handler then multiplied those three
    /// correlated numbers together, so a submission comfortably over the line on all three
    /// dimensions came back reading like a coin flip. It is derived here instead; these tests pin
    /// the properties that actually mattered to the user.
    /// </summary>
    public class GradingPassProbabilityTests
    {
        [Theory]
        [InlineData(1, "Band 3 or above", 0.97)]   // two bands clear of the line
        [InlineData(2, "Band 3 or above", 0.90)]   // one band clear
        [InlineData(2, "Band 2 or above", 0.62)]   // exactly on the line
        [InlineData(3, "Band 2 or above", 0.18)]   // one band short
        [InlineData(5, "Band 2 or above", 0.03)]   // hopeless
        public void Gap_to_the_pass_threshold_sets_the_base_rate(int band, string threshold, double expected)
        {
            var p = GradeSubmissionCommandHandler.EstimateDimensionPassProbability(
                band, threshold, confidence: "high", cumulativeDensityFlag: false);

            Assert.Equal((decimal)expected, p);
        }

        [Fact]
        public void A_band_comfortably_over_the_line_is_not_reported_as_a_coin_flip()
        {
            // The complaint that started this: judged band above the pass threshold, yet the
            // stored probability came back at 0.55.
            var p = GradeSubmissionCommandHandler.EstimateDimensionPassProbability(
                band: 2, passThreshold: "Band 3 or above", confidence: "high", cumulativeDensityFlag: false);

            Assert.True(p > 0.80m, $"expected a clear pass to read as a clear pass, got {p}");
        }

        [Fact]
        public void Low_confidence_pulls_the_estimate_toward_a_coin_flip_in_both_directions()
        {
            var confidentPass = GradeSubmissionCommandHandler.EstimateDimensionPassProbability(
                2, "Band 3 or above", "high", false);
            var unsurePass = GradeSubmissionCommandHandler.EstimateDimensionPassProbability(
                2, "Band 3 or above", "low", false);

            var confidentFail = GradeSubmissionCommandHandler.EstimateDimensionPassProbability(
                3, "Band 2 or above", "high", false);
            var unsureFail = GradeSubmissionCommandHandler.EstimateDimensionPassProbability(
                3, "Band 2 or above", "low", false);

            Assert.True(unsurePass < confidentPass);
            Assert.True(unsureFail > confidentFail);
        }

        [Fact]
        public void Two_dimensions_at_the_same_gap_can_still_differ()
        {
            // The "三个维度的分数会一样" complaint: with a gap-only table they could not.
            var certain = GradeSubmissionCommandHandler.EstimateDimensionPassProbability(
                2, "Band 2 or above", "high", false);
            var borderline = GradeSubmissionCommandHandler.EstimateDimensionPassProbability(
                2, "Band 2 or above", "medium", true);

            Assert.NotEqual(certain, borderline);
        }

        [Fact]
        public void On_the_line_plus_a_cumulative_density_flag_is_the_borderline_fail_shape()
        {
            var clean = GradeSubmissionCommandHandler.EstimateDimensionPassProbability(
                2, "Band 2 or above", "high", cumulativeDensityFlag: false);
            var dense = GradeSubmissionCommandHandler.EstimateDimensionPassProbability(
                2, "Band 2 or above", "high", cumulativeDensityFlag: true);

            Assert.Equal(0.62m, clean);
            Assert.Equal(0.52m, dense);
        }

        [Fact]
        public void An_unparseable_threshold_falls_back_to_no_information()
        {
            Assert.Equal(0.50m, GradeSubmissionCommandHandler.EstimateDimensionPassProbability(2, null, "high", false));
            Assert.Equal(0.50m, GradeSubmissionCommandHandler.EstimateDimensionPassProbability(2, "pass", "high", false));
        }

        [Fact]
        public void Three_clear_passes_do_not_compound_into_a_near_fail()
        {
            var results = Results(0.90m, 0.90m, 0.90m);

            var overall = GradeSubmissionCommandHandler.CombinePassProbability(results);

            // The plain product was 0.729 — this is the compounding bug the blend removes.
            Assert.True(overall > 0.84m, $"expected three clear passes to stay a clear pass, got {overall}");
            Assert.True(overall <= 0.90m, "overall can never exceed the weakest dimension");
        }

        [Fact]
        public void The_weakest_dimension_dominates_the_overall_estimate()
        {
            var overall = GradeSubmissionCommandHandler.CombinePassProbability(Results(0.97m, 0.97m, 0.18m));

            Assert.True(overall < 0.30m, $"a failing dimension must sink the whole submission, got {overall}");
        }

        [Fact]
        public void A_dimension_with_no_estimate_contributes_no_information()
        {
            Assert.Equal(1m, GradeSubmissionCommandHandler.CombinePassProbability(Results(null, null)));
            Assert.Equal(
                GradeSubmissionCommandHandler.CombinePassProbability(Results(0.62m)),
                GradeSubmissionCommandHandler.CombinePassProbability(Results(0.62m, null)));
        }

        private static List<GradingResult> Results(params decimal?[] probabilities)
            => probabilities.Select(p => new GradingResult { EstimatedPassProbability = p }).ToList();
    }
}
