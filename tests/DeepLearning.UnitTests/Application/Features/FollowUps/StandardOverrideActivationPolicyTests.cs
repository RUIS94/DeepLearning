using DeepLearning.Application.Features.FollowUps.Commands.CreateFollowUpQuestion;

namespace DeepLearning.UnitTests.Application.Features.FollowUps
{
    public class StandardOverrideActivationPolicyTests
    {
        [Theory]
        [InlineData(0, 3, false)]
        [InlineData(1, 3, false)]
        [InlineData(2, 3, false)]
        [InlineData(3, 3, true)]
        [InlineData(4, 3, true)]
        public void ShouldActivate_compares_confirmations_against_the_threshold(int confirmations, int threshold, bool expected)
        {
            Assert.Equal(expected, StandardOverrideActivationPolicy.ShouldActivate(confirmations, threshold));
        }

        [Fact]
        public void Default_threshold_matches_the_design_docs_own_example()
        {
            Assert.Equal(3, StandardOverrideActivationPolicy.DefaultConfirmationsRequired);
        }

        [Fact]
        public void Parses_the_seeded_policy_value_shape()
        {
            Assert.Equal(5, StandardOverrideActivationPolicy.ParseThreshold("{\"confirmations_required\": 5}"));
        }

        [Fact]
        public void Throws_when_confirmations_required_is_missing()
        {
            Assert.Throws<InvalidOperationException>(() => StandardOverrideActivationPolicy.ParseThreshold("{\"other_key\": 5}"));
        }

        [Fact]
        public void Throws_when_confirmations_required_is_not_positive()
        {
            Assert.Throws<InvalidOperationException>(() => StandardOverrideActivationPolicy.ParseThreshold("{\"confirmations_required\": 0}"));
        }
    }
}
