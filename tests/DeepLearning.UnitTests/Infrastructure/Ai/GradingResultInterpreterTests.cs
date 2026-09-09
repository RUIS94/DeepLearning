using DeepLearning.Infrastructure.Ai.GradingResultInterpreters;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    /// <summary>
    /// <see cref="Band15Interpreter"/> is the only scale NAATI CT actually uses and is validated
    /// against its real rubric. <see cref="Score100Interpreter"/> and
    /// <see cref="RubricLevelInterpreter"/> are deliberate placeholders (design doc §9.7 YAGNI) —
    /// they exist so <c>IGradingResultInterpreter</c> per-<c>scale_type</c> dispatch is real, but
    /// their bucketing has no real exam-type data behind it. These tests are the regression lock
    /// on that placeholder behaviour: if a second exam type ever adopts <c>score_0_100</c> or
    /// <c>rubric_level</c>, its real rubric should drive a rewrite here, not a silent drift.
    /// </summary>
    public class GradingResultInterpreterTests
    {
        public class Band15InterpreterTests
        {
            private readonly Band15Interpreter _interpreter = new();

            [Theory]
            [InlineData("1", "Band 2 or above", true)]
            [InlineData("2", "Band 2 or above", true)]
            [InlineData("3", "Band 2 or above", false)]
            [InlineData("3", "Band 3 or above", true)]
            public void Passes_when_the_reported_band_is_at_or_better_than_the_threshold_band(string rawBand, string passThreshold, bool expectedPass)
            {
                var result = _interpreter.Interpret(rawBand, passThreshold);

                Assert.Equal(int.Parse(rawBand), result.Band);
                Assert.Equal(expectedPass, result.PassBool);
            }

            [Fact]
            public void Passes_by_default_when_no_threshold_is_given()
            {
                var result = _interpreter.Interpret("4", null);

                Assert.True(result.PassBool);
            }

            [Fact]
            public void Throws_when_the_raw_value_has_no_parseable_band_number()
            {
                Assert.Throws<InvalidOperationException>(() => _interpreter.Interpret("no band", null));
            }
        }

        public class Score100InterpreterTests
        {
            private readonly Score100Interpreter _interpreter = new();

            [Theory]
            [InlineData("85", "60 or above", true)]
            [InlineData("45", "60 or above", false)]
            [InlineData("60", "60 or above", true)]
            public void Passes_when_the_score_is_at_or_above_the_threshold(string rawScore, string passThreshold, bool expectedPass)
            {
                var result = _interpreter.Interpret(rawScore, passThreshold);

                Assert.Equal(expectedPass, result.PassBool);
            }

            [Theory]
            [InlineData("95", 1)]
            [InlineData("80", 2)]
            [InlineData("65", 3)]
            [InlineData("45", 4)]
            [InlineData("10", 5)]
            // Exact cutoffs — the boundaries are inclusive on the upper band (>= 90 -> 1, etc.).
            [InlineData("90", 1)]
            [InlineData("75", 2)]
            [InlineData("60", 3)]
            [InlineData("40", 4)]
            [InlineData("89.9", 2)]
            public void Buckets_the_percentage_score_into_a_1_to_5_band_for_storage(string rawScore, int expectedBand)
            {
                var result = _interpreter.Interpret(rawScore, null);

                Assert.Equal(expectedBand, result.Band);
            }

            [Fact]
            public void Throws_when_the_raw_value_has_no_parseable_number()
            {
                Assert.Throws<InvalidOperationException>(() => _interpreter.Interpret("not scored", null));
            }
        }

        public class RubricLevelInterpreterTests
        {
            private readonly RubricLevelInterpreter _interpreter = new();

            [Theory]
            [InlineData("2", "3", true)]
            [InlineData("4", "3", false)]
            [InlineData("3", "3", true)]
            public void Passes_when_the_reported_level_is_at_or_better_than_the_threshold_level(string rawLevel, string passThreshold, bool expectedPass)
            {
                var result = _interpreter.Interpret(rawLevel, passThreshold);

                Assert.Equal(int.Parse(rawLevel), result.Band);
                Assert.Equal(expectedPass, result.PassBool);
            }

            [Fact]
            public void Passes_by_default_when_no_threshold_is_given()
            {
                Assert.True(_interpreter.Interpret("5", null).PassBool);
            }

            [Fact]
            public void Throws_when_the_raw_value_has_no_parseable_level_number()
            {
                Assert.Throws<InvalidOperationException>(() => _interpreter.Interpret("unrated", null));
            }
        }
    }
}
