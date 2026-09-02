using System.Text.Json;
using DeepLearning.Application.Features.Questions.Commands.GenerateQuestion;

namespace DeepLearning.UnitTests.Application.Features.Questions
{
    public class TopicDistributionSelectorTests
    {
        [Theory]
        [InlineData(0.5, 0.1, true)]
        [InlineData(0.5, 0.49, true)]
        [InlineData(0.5, 0.5, false)]
        [InlineData(0.5, 0.9, false)]
        [InlineData(0.0, 0.0, false)]
        [InlineData(1.0, 0.999, true)]
        public void ShouldPick_compares_the_roll_against_the_ratio(double ratio, double roll, bool expected)
        {
            Assert.Equal(expected, TopicDistributionSelector.ShouldPick(ratio, roll));
        }

        [Fact]
        public void ParseRatio_reads_topic_random_ratio_from_the_seeded_policy_shape()
        {
            var ratio = TopicDistributionSelector.ParseRatio("""{"topic_random_ratio": 0.5}""");

            Assert.Equal(0.5, ratio);
        }

        [Fact]
        public void ParseRatio_falls_back_to_the_default_when_the_key_is_missing()
        {
            var ratio = TopicDistributionSelector.ParseRatio("""{"something_else": 0.9}""");

            Assert.Equal(TopicDistributionSelector.DefaultTopicRandomRatio, ratio);
        }

        [Fact]
        public void ParseRatio_throws_for_invalid_json()
        {
            Assert.Throws<JsonException>(() => TopicDistributionSelector.ParseRatio("not json"));
        }
    }
}
