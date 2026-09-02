using DeepLearning.Infrastructure.Ai;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    /// <summary>
    /// AiTraceRenderer is the "make it human-readable" half of AI prompt tracing: it must decode
    /// \uXXXX escapes, keep real line breaks, pull the system/user prompts out of both request
    /// shapes and the assistant text out of both response shapes, and never drop content it does
    /// not recognise (fall back to pretty JSON instead).
    /// </summary>
    public class AiTraceRendererTests
    {
        [Fact]
        public void RenderRequest_openai_shape_decodes_escapes_and_labels_roles()
        {
            var json = "{\"model\":\"mimo-v2.5-pro\",\"max_completion_tokens\":8192,"
                + "\"messages\":[{\"role\":\"system\",\"content\":\"\\u4f60\\u662f\\u8bd1\\u5458\"},"
                + "{\"role\":\"user\",\"content\":\"\\u8bf7\\u7ffb\\u8bd1\\uff1aHello\"}]}";

            var rendered = AiTraceRenderer.RenderRequest(json);

            Assert.Contains("model: mimo-v2.5-pro", rendered);
            Assert.Contains("max_completion_tokens: 8192", rendered);
            Assert.Contains("### SYSTEM\n你是译员", rendered.Replace("\r\n", "\n"));
            Assert.Contains("### USER\n请翻译：Hello", rendered.Replace("\r\n", "\n"));
            Assert.DoesNotContain("\\u", rendered);
        }

        [Fact]
        public void RenderRequest_claude_shape_pulls_top_level_system()
        {
            var json = "{\"model\":\"claude-opus-5\",\"max_tokens\":4096,\"system\":\"\\u7cfb\\u7edf\\u63d0\\u793a\","
                + "\"messages\":[{\"role\":\"user\",\"content\":\"\\u95ee\\u9898\"}],\"thinking\":{\"type\":\"disabled\"}}";

            var rendered = AiTraceRenderer.RenderRequest(json).Replace("\r\n", "\n");

            Assert.Contains("thinking: {\"type\":\"disabled\"}", rendered);
            Assert.Contains("### SYSTEM\n系统提示", rendered);
            Assert.Contains("### USER\n问题", rendered);
        }

        [Fact]
        public void RenderResponse_openai_shape_extracts_assistant_text_and_reasoning()
        {
            var json = "{\"choices\":[{\"message\":{\"reasoning_content\":\"\\u601d\\u8003\",\"content\":\"\\u6700\\u7ec8\\u56de\\u7b54\"},"
                + "\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5}}";

            var rendered = AiTraceRenderer.RenderResponse(json).Replace("\r\n", "\n");

            Assert.Contains("### ASSISTANT (thinking)\n思考", rendered);
            Assert.Contains("### ASSISTANT\n最终回答", rendered);
            Assert.Contains("finish_reason: stop", rendered);
            Assert.Contains("usage: {\"prompt_tokens\":10,\"completion_tokens\":5}", rendered);
        }

        [Fact]
        public void RenderResponse_claude_shape_extracts_thinking_and_text_blocks()
        {
            var json = "{\"content\":[{\"type\":\"thinking\",\"thinking\":\"\\u63a8\\u7406\"},"
                + "{\"type\":\"text\",\"text\":\"\\u7b54\\u6848\"}],\"stop_reason\":\"end_turn\"}";

            var rendered = AiTraceRenderer.RenderResponse(json).Replace("\r\n", "\n");

            Assert.Contains("### ASSISTANT (thinking)\n推理", rendered);
            Assert.Contains("### ASSISTANT\n答案", rendered);
            Assert.Contains("stop_reason: end_turn", rendered);
        }

        [Fact]
        public void RenderResponse_error_shape_is_surfaced()
        {
            var rendered = AiTraceRenderer.RenderResponse("{\"error\":{\"message\":\"bad key\",\"type\":\"auth\"}}");

            Assert.Contains("### ERROR", rendered);
            Assert.Contains("bad key", rendered);
        }

        [Fact]
        public void Unrecognised_shapes_fall_back_to_pretty_json_not_silence()
        {
            var rendered = AiTraceRenderer.RenderResponse("{\"something\":\"\\u5b8c\\u5168\\u9646\\u751f\"}");

            Assert.Contains("完全陆生", rendered);
        }

        [Fact]
        public void Non_json_input_is_returned_verbatim()
        {
            Assert.Equal("not json at all", AiTraceRenderer.RenderRequest("not json at all"));
            Assert.Equal("(no request body)", AiTraceRenderer.RenderRequest(""));
        }
    }
}
