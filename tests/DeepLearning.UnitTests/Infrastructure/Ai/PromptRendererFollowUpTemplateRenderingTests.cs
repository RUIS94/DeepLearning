using DeepLearning.Infrastructure.Ai;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    /// <summary>
    /// add_followup_prompt_template.sql's self-review checklist block (Step 5) is dense Chinese
    /// prose with parentheses/colons/slashes right next to a `{{ if task_type == "B" }}` block —
    /// nothing else in the codebase had rendered a Scriban conditional wrapped in that much
    /// punctuation before, so this pins down that it parses and renders correctly (and that the
    /// TaskB-only bullet actually appears/disappears based on task_type) through the same
    /// PromptRenderer.Render(model) call CreateFollowUpQuestionCommandHandler uses, same
    /// precedent as PromptRendererDimensionRenderingTests for the grading template.
    /// </summary>
    public class PromptRendererFollowUpTemplateRenderingTests
    {
        // Mirrors the relevant slice of add_followup_prompt_template.sql's 重要说明 block —
        // not the whole template, just the part introduced by the self-review checklist expansion.
        private const string ChecklistTemplate = """
            - 漏判:用户译文中实际存在的错误/细节,AI没有发现
            - 误判(false positive):用户译文其实站得住脚,AI却当成了错误扣分——尤其警惕把自己"更偏好"的某种措辞
              当成了隐形标准答案,排斥掉其他同样合规的译法
            {{ if task_type == "B" }}
            - (本题为TaskB)位置定位错:把用户标注的位置错误地匹配到了另一个预设错误上,导致误判用户"找错了/漏找了"
            {{ end }}
            结束。
            """;

        [Fact]
        public void Renders_the_taskb_only_bullet_when_task_type_is_b()
        {
            var renderer = new PromptRenderer();

            var result = renderer.Render(ChecklistTemplate, new { TaskType = "B" });

            Assert.Contains("位置定位错", result);
            Assert.Contains("结束。", result);
        }

        [Fact]
        public void Omits_the_taskb_only_bullet_when_task_type_is_a()
        {
            var renderer = new PromptRenderer();

            var result = renderer.Render(ChecklistTemplate, new { TaskType = "A" });

            Assert.DoesNotContain("位置定位错", result);
            Assert.Contains("误判(false positive)", result);
            Assert.Contains("结束。", result);
        }

        // Mirrors add_followup_thread_history_prompt_template.sql's 【本线程此前的对话】 block —
        // multi-round follow-up threads (2026-09-02) replay prior turns to the AI as `history`,
        // a list of { role, content } exactly as FollowUpThreadSupport.BuildTemplateModel emits it.
        private const string HistoryTemplate = """
            {{ if history.size > 0 }}
            此前的对话:
            {{ for m in history }}
            {{ if m.role == "user" }}用户: {{ else }}AI: {{ end }}{{ m.content }}
            {{ end }}
            {{ end }}
            当前追问: {{ question_text }}
            """;

        [Fact]
        public void Renders_the_thread_history_block_in_role_order()
        {
            var renderer = new PromptRenderer();

            var result = renderer.Render(HistoryTemplate, new
            {
                QuestionText = "第三轮问题",
                History = new[]
                {
                    new { Role = "user", Content = "第一轮问题" },
                    new { Role = "ai", Content = "第一轮回答" },
                },
            });

            Assert.Contains("用户: 第一轮问题", result);
            Assert.Contains("AI: 第一轮回答", result);
            Assert.Contains("当前追问: 第三轮问题", result);
            Assert.True(result.IndexOf("第一轮问题", StringComparison.Ordinal) < result.IndexOf("第一轮回答", StringComparison.Ordinal));
        }

        [Fact]
        public void Omits_the_history_block_on_the_first_round()
        {
            var renderer = new PromptRenderer();

            var result = renderer.Render(HistoryTemplate, new
            {
                QuestionText = "第一轮问题",
                History = Array.Empty<object>(),
            });

            Assert.DoesNotContain("此前的对话", result);
            Assert.Contains("当前追问: 第一轮问题", result);
        }
    }
}
