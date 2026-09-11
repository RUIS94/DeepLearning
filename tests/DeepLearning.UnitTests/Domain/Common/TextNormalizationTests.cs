using DeepLearning.Domain.Common;

namespace DeepLearning.UnitTests.Domain.Common
{
    public class TextNormalizationTests
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("   ", null)]
        [InlineData("Hello", "hello")]
        [InlineData("  leading and trailing  ", "leading and trailing")]
        [InlineData("multiple   internal    spaces", "multiple internal spaces")]
        [InlineData("in\tlight\nof", "in light of")]
        [InlineData("CJK 中文 mixed", "cjk 中文 mixed")]
        public void CanonicalKey_lowercases_trims_and_collapses_whitespace(string? input, string? expected)
        {
            Assert.Equal(expected, TextNormalization.CanonicalKey(input));
        }

        [Fact]
        public void CanonicalKey_caps_at_255_characters()
        {
            var input = new string('a', 300);

            var result = TextNormalization.CanonicalKey(input);

            Assert.NotNull(result);
            Assert.Equal(255, result!.Length);
            Assert.Equal(new string('a', 255), result);
        }

        [Fact]
        public void CanonicalKey_caps_after_collapsing_whitespace_not_before()
        {
            // Raw length (598, with doubled spaces) is well over 255, but the collapsed content
            // is also over 255 — this pins that collapsing happens before the cap is applied, not
            // that a merely-long-looking raw string gets truncated mid-collapse.
            var input = string.Join("  ", Enumerable.Repeat("ab", 150));

            var result = TextNormalization.CanonicalKey(input);

            Assert.NotNull(result);
            Assert.Equal(255, result!.Length);
            Assert.DoesNotContain("  ", result);
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("Hello, World!", "HelloWorld")]
        [InlineData("state-of-the-art", "stateoftheart")]
        [InlineData("Rule 34 applies.", "Rule34applies")]
        [InlineData("— …", "")]
        public void StripToAlphanumeric_keeps_only_letters_and_digits_and_preserves_case(string? input, string expected)
        {
            Assert.Equal(expected, TextNormalization.StripToAlphanumeric(input));
        }
    }
}
