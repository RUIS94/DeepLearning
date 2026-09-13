using System.Text.Json;
using DeepLearning.Application.Common;

namespace DeepLearning.UnitTests.Application.Common
{
    public class LlmJsonTests
    {
        private record Finding(string ErrorCategory, string Explanation);

        private record Payload(List<Finding> Findings);

        [Fact]
        public void Parse_handles_well_formed_json_without_repair()
        {
            var json = """{"findings":[{"errorCategory":"distortion","explanation":"plain text"}]}""";

            var result = LlmJson.Parse<Payload>(json);

            Assert.Single(result.Findings);
            Assert.Equal("plain text", result.Findings[0].Explanation);
        }

        [Fact]
        public void Parse_repairs_unescaped_ascii_quote_around_a_quoted_term()
        {
            // The exact observed failure shape: the model quotes "program" with a literal ASCII
            // '"' instead of escaping it, ending the string early and derailing the parser on the
            // Chinese prose that follows — this is what produced the "'0xE7' is invalid after a
            // value" error (0xE7 being the lead byte of 绝, mid-way through unrelated text).
            var json = "{\"findings\":[{\"errorCategory\":\"distortion\","
                + "\"explanation\":\"原文中\"program\"一词被漏译\"}]}";

            var result = LlmJson.Parse<Payload>(json);

            Assert.Single(result.Findings);
            Assert.Equal("原文中\"program\"一词被漏译", result.Findings[0].Explanation);
        }

        [Fact]
        public void Parse_repairs_multiple_stray_quotes_in_the_same_string()
        {
            var json = "{\"findings\":[{\"errorCategory\":\"distortion\","
                + "\"explanation\":\"应译为\"更正\"而非\"原文\"\"}]}";

            var result = LlmJson.Parse<Payload>(json);

            Assert.Equal("应译为\"更正\"而非\"原文\"", result.Findings[0].Explanation);
        }

        [Fact]
        public void Parse_repairs_raw_newline_inside_a_string_value()
        {
            var json = "{\"findings\":[{\"errorCategory\":\"distortion\",\"explanation\":\"line one\nline two\"}]}";

            var result = LlmJson.Parse<Payload>(json);

            Assert.Equal("line one\nline two", result.Findings[0].Explanation);
        }

        [Fact]
        public void Parse_still_throws_when_json_is_unrecoverably_malformed()
        {
            var json = "{\"findings\": [ this is not json at all";

            Assert.ThrowsAny<JsonException>(() => LlmJson.Parse<Payload>(json));
        }

        [Fact]
        public void Parse_strips_markdown_fence_before_repairing()
        {
            var json = "```json\n{\"findings\":[{\"errorCategory\":\"distortion\","
                + "\"explanation\":\"引用\"术语\"示例\"}]}\n```";

            var result = LlmJson.Parse<Payload>(json);

            Assert.Equal("引用\"术语\"示例", result.Findings[0].Explanation);
        }
    }
}
