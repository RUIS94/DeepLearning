using System.Text.Json;
using DeepLearning.Application.Features.Questions.Commands.GenerateQuestion;

namespace DeepLearning.UnitTests.Application.Features.Questions
{
    public class WeakPointTargetingSelectorTests
    {
        [Theory]
        [InlineData(0.3, 0.1, true)]
        [InlineData(0.3, 0.29, true)]
        [InlineData(0.3, 0.3, false)]
        [InlineData(0.3, 0.9, false)]
        [InlineData(0.0, 0.0, false)]
        [InlineData(1.0, 0.999, true)]
        public void ShouldTarget_compares_the_roll_against_the_ratio(double ratio, double roll, bool expected)
        {
            Assert.Equal(expected, WeakPointTargetingSelector.ShouldTarget(ratio, roll));
        }

        [Fact]
        public void ParseRatio_reads_weak_point_ratio_from_the_seeded_policy_shape()
        {
            var ratio = WeakPointTargetingSelector.ParseRatio("""{"weak_point_ratio": 0.3, "random_ratio": 0.7}""");

            Assert.Equal(0.3, ratio);
        }

        [Fact]
        public void ParseRatio_falls_back_to_the_default_when_the_key_is_missing()
        {
            var ratio = WeakPointTargetingSelector.ParseRatio("""{"something_else": 0.5}""");

            Assert.Equal(WeakPointTargetingSelector.DefaultWeakPointRatio, ratio);
        }

        [Fact]
        public void ParseRatio_throws_for_invalid_json()
        {
            // Malformed JSON fails inside JsonSerializer.Deserialize itself (JsonException) —
            // the InvalidOperationException path only covers "valid JSON that deserialized to
            // null", a different failure mode.
            Assert.Throws<JsonException>(() => WeakPointTargetingSelector.ParseRatio("not json"));
        }
    }
}
