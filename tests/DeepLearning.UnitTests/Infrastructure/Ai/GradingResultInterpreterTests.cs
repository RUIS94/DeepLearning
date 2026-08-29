using DeepLearning.Infrastructure.Ai.GradingResultInterpreters;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
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
            public void Buckets_the_percentage_score_into_a_1_to_5_band_for_storage(string rawScore, int expectedBand)
            {
                var result = _interpreter.Interpret(rawScore, null);

                Assert.Equal(expectedBand, result.Band);
            }
        }

        public class RubricLevelInterpreterTests
        {
            private readonly RubricLevelInterpreter _interpreter = new();

            [Theory]
            [InlineData("2", "3", true)]
            [InlineData("4", "3", false)]
            public void Passes_when_the_reported_level_is_at_or_better_than_the_threshold_level(string rawLevel, string passThreshold, bool expectedPass)
            {
                var result = _interpreter.Interpret(rawLevel, passThreshold);

                Assert.Equal(expectedPass, result.PassBool);
            }
        }
    }
}
