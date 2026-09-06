using System.Text.RegularExpressions;
using DeepLearning.Infrastructure.Ai;
using DeepLearning.Infrastructure.Persistence.Sql;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    /// <summary>
    /// Renders the ACTUAL shipped deep_learning template (the single production v1 row that
    /// reset_deep_learning_prompt_v1_exam_specific.sql inserts) through <see cref="PromptRenderer"/>
    /// with the exact anonymous-model shape GenerateDeepLearningContentCommandHandler builds
    /// (SourceTitle + TaskType + SourceText — the cross-question prior_vocab block was removed).
    /// Same "exercise the exact Scriban shape of the real artifact" precedent as
    /// PromptRendererReferenceTranslationRenderingTests / PromptRendererFollowUpTemplateRenderingTests —
    /// a stray `{{` in a future prose edit is caught here instead of in a live generation.
    /// </summary>
    public class PromptRendererDeepLearningTemplateTests
    {
        private static string ShippedTemplate()
        {
            var script = new EmbeddedSqlScriptSource().GetScripts()
                .Single(s => s.Name == "reset_deep_learning_prompt_v1_exam_specific.sql");
            var match = Regex.Match(script.Content, @"\$tpl\$(.*)\$tpl\$", RegexOptions.Singleline);
            Assert.True(match.Success, "reset_deep_learning_prompt_v1_exam_specific.sql must dollar-quote its template body with $tpl$");
            return match.Groups[1].Value;
        }

        [Theory]
        [InlineData("A", "Applicants aged 65 and over may be eligible for a concession card.")]
        [InlineData("B", "Those who do not opt in by the closing date will be enrolled automatically.")]
        public void Renders_with_a_source_title(string taskType, string sourceText)
        {
            var rendered = new PromptRenderer().Render(ShippedTemplate(), new
            {
                TaskType = taskType,
                SourceTitle = "Green IT: A Cost Cutting Strategy",
                SourceText = sourceText,
            });

            Assert.Contains(sourceText, rendered);
            Assert.Contains("任务类型:" + taskType, rendered);
            Assert.Contains("【原文标题】", rendered);
            Assert.Contains("Green IT: A Cost Cutting Strategy", rendered);
            Assert.Contains("0. referenceTitle", rendered);   // the title-translation instruction is shown
            Assert.Contains("\"referenceTitle\"", rendered);
            Assert.Contains("\"referenceText\"", rendered);
            Assert.Contains("\"vocabExpressions\"", rendered);
            Assert.DoesNotContain("{{", rendered);
            Assert.DoesNotContain("以下表达此前已在其它篇目积累过", rendered);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Omits_the_title_block_when_the_source_has_no_title(string? title)
        {
            var rendered = new PromptRenderer().Render(ShippedTemplate(), new
            {
                TaskType = "A",
                SourceTitle = title,
                SourceText = "A short passage without a title.",
            });

            Assert.DoesNotContain("【原文标题】", rendered);
            Assert.DoesNotContain("(含标题)", rendered);
            Assert.DoesNotContain("0. referenceTitle", rendered);   // instruction hidden when no title
            Assert.Contains("A short passage without a title.", rendered);
            Assert.DoesNotContain("{{", rendered);
        }
    }
}
