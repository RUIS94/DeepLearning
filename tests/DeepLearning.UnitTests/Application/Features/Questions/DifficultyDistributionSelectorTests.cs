using DeepLearning.Application.Features.Questions.Commands.GenerateQuestion;
using DeepLearning.Domain.Enums;

namespace DeepLearning.UnitTests.Application.Features.Questions
{
    public class DifficultyDistributionSelectorTests
    {
        private static readonly IReadOnlyDictionary<Difficulty, decimal> Weights = new Dictionary<Difficulty, decimal>
        {
            [Difficulty.easy] = 0.3m,
            [Difficulty.medium] = 0.5m,
            [Difficulty.hard] = 0.2m,
        };

        [Theory]
        [InlineData(0.0, Difficulty.easy)]
        [InlineData(0.29, Difficulty.easy)]
        [InlineData(0.3, Difficulty.medium)]
        [InlineData(0.5, Difficulty.medium)]
        [InlineData(0.79, Difficulty.medium)]
        [InlineData(0.8, Difficulty.hard)]
        [InlineData(0.99, Difficulty.hard)]
        public void Picks_the_bucket_the_roll_falls_into(double roll, Difficulty expected)
        {
            Assert.Equal(expected, DifficultyDistributionSelector.Select(Weights, roll));
        }

        [Fact]
        public void Handles_a_roll_right_at_the_top_of_the_range()
        {
            // Guards against floating-point rounding leaving no bucket matched at roll ~= 1.0.
            Assert.Equal(Difficulty.hard, DifficultyDistributionSelector.Select(Weights, 0.999999999));
        }

        [Fact]
        public void Throws_when_weights_sum_to_zero()
        {
            var zeroWeights = new Dictionary<Difficulty, decimal> { [Difficulty.easy] = 0m };

            Assert.Throws<InvalidOperationException>(() => DifficultyDistributionSelector.Select(zeroWeights, 0.5));
        }

        [Fact]
        public void Default_weights_match_the_design_docs_30_50_20_ratio()
        {
            Assert.Equal(0.3m, DifficultyDistributionSelector.DefaultWeights[Difficulty.easy]);
            Assert.Equal(0.5m, DifficultyDistributionSelector.DefaultWeights[Difficulty.medium]);
            Assert.Equal(0.2m, DifficultyDistributionSelector.DefaultWeights[Difficulty.hard]);
        }

        [Fact]
        public void Parses_the_seeded_policy_value_shape()
        {
            var weights = DifficultyDistributionSelector.ParseWeights("{\"easy\": 0.3, \"medium\": 0.5, \"hard\": 0.2}");

            Assert.Equal(0.3m, weights[Difficulty.easy]);
            Assert.Equal(0.5m, weights[Difficulty.medium]);
            Assert.Equal(0.2m, weights[Difficulty.hard]);
        }

        [Fact]
        public void Ignores_unrecognized_keys_when_parsing()
        {
            var weights = DifficultyDistributionSelector.ParseWeights("{\"easy\": 0.3, \"not_a_difficulty\": 0.7}");

            Assert.Single(weights);
            Assert.Equal(0.3m, weights[Difficulty.easy]);
        }

        [Fact]
        public void Throws_when_parsing_a_value_with_no_recognized_keys()
        {
            Assert.Throws<InvalidOperationException>(() => DifficultyDistributionSelector.ParseWeights("{\"not_a_difficulty\": 1.0}"));
        }
    }
}
