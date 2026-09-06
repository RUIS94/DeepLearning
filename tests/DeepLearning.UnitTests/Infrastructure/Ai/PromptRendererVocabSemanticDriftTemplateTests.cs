using System.Text.RegularExpressions;
using DeepLearning.Infrastructure.Ai;
using DeepLearning.Infrastructure.Persistence.Sql;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    /// <summary>
    /// Renders the shipped vocab_semantic_drift template (from
    /// add_vocab_semantic_drift_prompt_template.sql) through <see cref="PromptRenderer"/> with the
    /// exact anonymous-model shape VocabSemanticDriftService builds — a stray `{{` in a future
    /// prose edit is caught here rather than in a live background run.
    /// </summary>
    public class PromptRendererVocabSemanticDriftTemplateTests
    {
        private static string ShippedTemplate()
        {
            var script = new EmbeddedSqlScriptSource().GetScripts()
                .Single(s => s.Name == "add_vocab_semantic_drift_prompt_template.sql");
            var match = Regex.Match(script.Content, @"\$tpl\$(.*)\$tpl\$", RegexOptions.Singleline);
            Assert.True(match.Success, "add_vocab_semantic_drift_prompt_template.sql must dollar-quote its template body with $tpl$");
            return match.Groups[1].Value;
        }

        [Fact]
        public void Renders_the_items_loop_cleanly()
        {
            var rendered = new PromptRenderer().Render(ShippedTemplate(), new
            {
                TaskType = "A",
                SourceText = "Applicants may pay the fee in six equal monthly instalments.",
                Items = new[]
                {
                    new { EnglishExpr = "instalment", ThisChinese = (string?)"分期款", ThisNote = (string?)"此处指分期付款的一期", KnownSemantics = "连载作品的一集" },
                    new { EnglishExpr = "eligible for", ThisChinese = (string?)null, ThisNote = (string?)null, KnownSemantics = "" },
                },
            });

            Assert.Contains("instalment", rendered);
            Assert.Contains("分期款", rendered);
            Assert.Contains("连载作品的一集", rendered);
            Assert.Contains("\"results\"", rendered);
            Assert.DoesNotContain("{{", rendered);
        }
    }
}
