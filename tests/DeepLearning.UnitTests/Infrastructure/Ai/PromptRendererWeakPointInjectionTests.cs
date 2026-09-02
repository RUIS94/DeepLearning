using DeepLearning.Infrastructure.Ai;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    /// <summary>
    /// Pins down the two conditional blocks M9
    /// (fix_grading_inject_weak_points_and_overrides_prompt_template.sql) adds to the grading
    /// shared_methodology template: <c>{{ if weak_points.size &gt; 0 }}</c> /
    /// <c>{{ if active_overrides.size &gt; 0 }}</c> rendered against the shape
    /// GradeSubmissionCommandHandler.BuildTemplateModel now produces. Same PromptRenderer.Render
    /// path as production.
    /// </summary>
    public class PromptRendererWeakPointInjectionTests
    {
        private const string WeakPointBlock =
            "{{ if weak_points.size > 0 }}\n历史薄弱点:\n{{ for w in weak_points }}\n- {{ w.name }}{{ if w.recurring }}(复发){{ end }}:{{ w.description }}\n{{ end }}\n{{ end }}";

        private const string OverrideBlock =
            "{{ if active_overrides.size > 0 }}\n修正补丁:\n{{ for o in active_overrides }}\n- [{{ o.scope }} / {{ o.dimension_or_rule }}] {{ o.revised_rule_text }}\n{{ end }}\n{{ end }}";

        [Fact]
        public void Renders_weak_points_with_recurring_marker()
        {
            var renderer = new PromptRenderer();
            var model = new
            {
                WeakPoints = new[]
                {
                    new { Name = "省略保留性/让步语气词", Description = "把 some/likely 译得过于肯定", Recurring = true },
                    new { Name = "数字陷阱", Description = "倍数换算", Recurring = false },
                },
                ActiveOverrides = Array.Empty<object>(),
            };

            var result = renderer.Render(WeakPointBlock, model);

            Assert.Contains("- 省略保留性/让步语气词(复发):把 some/likely 译得过于肯定", result);
            Assert.Contains("- 数字陷阱:倍数换算", result);
            // The non-recurring item must NOT carry the marker.
            Assert.DoesNotContain("数字陷阱(复发)", result);
        }

        [Fact]
        public void Renders_active_overrides_with_scope_and_rule()
        {
            var renderer = new PromptRenderer();
            var model = new
            {
                WeakPoints = Array.Empty<object>(),
                ActiveOverrides = new[]
                {
                    new { Scope = "grading_rubric", DimensionOrRule = "meaning_transfer", RevisedRuleText = "枚举型 from A to B 不按遗漏扣分" },
                },
            };

            var result = renderer.Render(OverrideBlock, model);

            Assert.Contains("- [grading_rubric / meaning_transfer] 枚举型 from A to B 不按遗漏扣分", result);
        }

        [Fact]
        public void Both_blocks_collapse_to_nothing_when_lists_are_empty()
        {
            var renderer = new PromptRenderer();
            var model = new { WeakPoints = Array.Empty<object>(), ActiveOverrides = Array.Empty<object>() };

            Assert.Equal(string.Empty, renderer.Render(WeakPointBlock, model).Trim());
            Assert.Equal(string.Empty, renderer.Render(OverrideBlock, model).Trim());
        }

        // The exact block add_question_gen_topic_hint_prompt_template.sql seeds. That row is not
        // loaded into the integration test DB, so pin its Scriban validity here.
        private const string TopicHintBlock =
            "\n{{ if topic_hint }}\n【题材倾向】本次出题优先选取「{{ topic_hint }}」领域的真实文章。若该领域确实没有合适的真实素材,可适当放宽,但不要为贴合题材而牺牲文章的真实性与自然度。\n{{ end }}\n";

        [Fact]
        public void Topic_hint_block_renders_when_set_and_collapses_when_null()
        {
            var renderer = new PromptRenderer();

            var withHint = renderer.Render(TopicHintBlock, new { TopicHint = "环境政策" });
            Assert.Contains("【题材倾向】本次出题优先选取「环境政策」领域的真实文章。", withHint);

            var withoutHint = renderer.Render(TopicHintBlock, new { TopicHint = (string?)null });
            Assert.Equal(string.Empty, withoutHint.Trim());
        }

        // The prior-vocab dedup block improve_deep_learning_dedup_prompt_template.sql seeds into
        // the deep_learning template — nested if/for/if, not in the integration test DB.
        private const string PriorVocabBlock =
            "{{ if prior_vocab.size > 0 }}\n【以下表达此前已在其它篇目积累过】\n{{ for pv in prior_vocab }}\n- {{ pv.english_expr }}{{ if pv.chinese_equiv }}({{ pv.chinese_equiv }}){{ end }}{{ if pv.context_note }} —— 旧笔记:{{ pv.context_note }}{{ end }}\n{{ end }}\n{{ end }}";

        [Fact]
        public void Prior_vocab_block_renders_nested_items_and_collapses_when_empty()
        {
            var renderer = new PromptRenderer();

            var withItems = renderer.Render(PriorVocabBlock, new
            {
                PriorVocab = new[]
                {
                    new { EnglishExpr = "in the wake of", ChineseEquiv = "在……之后", ContextNote = "多用于负面事件后" },
                    new { EnglishExpr = "subject to", ChineseEquiv = (string?)null, ContextNote = (string?)null },
                },
            });
            Assert.Contains("- in the wake of(在……之后) —— 旧笔记:多用于负面事件后", withItems);
            Assert.Contains("- subject to\n", withItems);

            var empty = renderer.Render(PriorVocabBlock, new { PriorVocab = Array.Empty<object>() });
            Assert.Equal(string.Empty, empty.Trim());
        }

        // The two loops add_weak_point_classification_prompt_template.sql (M10) renders against
        // the shape WeakPointClassifier builds — not in the integration test DB.
        private const string ClassificationLoops =
            "【错误清单】\n{{ for e in errors }}\n- errorId={{ e.error_id }} | 维度={{ e.dimension_key }} | 类别={{ e.error_category_key }}\n  片段:{{ e.snippet }}\n{{ end }}\n【清单】\n{{ for c in catalog }}\n- {{ c.code }} = {{ c.name }}:{{ c.description }}\n{{ end }}";

        [Fact]
        public void Weak_point_classification_loops_render_errors_and_catalog()
        {
            var renderer = new PromptRenderer();
            var result = renderer.Render(ClassificationLoops, new
            {
                Errors = new[]
                {
                    new { ErrorId = "11111111-1111-1111-1111-111111111111", DimensionKey = "meaning_transfer", ErrorCategoryKey = "distortion", Snippet = "翻两倍" },
                },
                Catalog = new[]
                {
                    new { Code = "numeric_statistical_traps", Name = "数字/统计类陷阱", Description = "倍数换算等" },
                },
            });

            Assert.Contains("errorId=11111111-1111-1111-1111-111111111111 | 维度=meaning_transfer | 类别=distortion", result);
            Assert.Contains("片段:翻两倍", result);
            Assert.Contains("- numeric_statistical_traps = 数字/统计类陷阱:倍数换算等", result);
        }
    }
}
