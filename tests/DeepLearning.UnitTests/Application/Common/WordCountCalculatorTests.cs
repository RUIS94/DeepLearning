using DeepLearning.Application.Common;

namespace DeepLearning.UnitTests.Application.Common
{
    public class WordCountCalculatorTests
    {
        [Theory]
        [InlineData(null, 0)]
        [InlineData("", 0)]
        [InlineData("   \n\t ", 0)]
        [InlineData("word", 1)]
        [InlineData("The quick brown fox jumps over the lazy dog.", 9)]
        [InlineData("  leading and   trailing   spaces  ", 4)]
        [InlineData("state-of-the-art", 4)]
        [InlineData("Rule 34 applies", 3)]
        [InlineData("— …", 0)]
        public void Counts_latin_words_as_letter_or_digit_runs(string? text, int expected)
        {
            Assert.Equal(expected, WordCountCalculator.Count(text));
        }

        [Theory]
        [InlineData("今天天气很好", 6)]
        [InlineData("你好，世界！", 4)]
        [InlineData("NAATI CT 英译中考试", 7)] // NAATI + CT + 英译中考试(5 ideographs)
        [InlineData("混合 mixed 文本 text", 6)] // 混+合 + mixed + 文+本 + text
        public void Counts_each_cjk_character_as_one_word(string text, int expected)
        {
            Assert.Equal(expected, WordCountCalculator.Count(text));
        }
    }
}
