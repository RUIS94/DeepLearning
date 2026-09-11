using DeepLearning.Application.Common;

namespace DeepLearning.UnitTests.Application.Common
{
    public class GradingScaleParsingTests
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("   ", null)]
        [InlineData("2", 2)]
        [InlineData("Band 2", 2)]
        [InlineData("Band 2 or above", 2)]
        [InlineData("Band 2.9 or above", 2)]
        [InlineData("-3", -3)]
        [InlineData("no band here", null)]
        public void ExtractLeadingInt_pulls_the_first_number_out_of_a_grading_scale_string(string? text, int? expected)
        {
            Assert.Equal(expected, GradingScaleParsing.ExtractLeadingInt(text));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("60", 60.0)]
        [InlineData("89.9", 89.9)]
        [InlineData("60 or above", 60.0)]
        [InlineData("-1.5", -1.5)]
        [InlineData("not scored", null)]
        public void ExtractLeadingDecimal_pulls_the_first_decimal_number_out_of_a_grading_scale_string(string? text, double? expected)
        {
            Assert.Equal(expected is null ? null : (decimal?)expected, GradingScaleParsing.ExtractLeadingDecimal(text));
        }
    }
}
