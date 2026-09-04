using System.Text.RegularExpressions;
using DeepLearning.Infrastructure.Ai;
using DeepLearning.Infrastructure.Persistence.Sql;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    /// <summary>
    /// The same guard <see cref="PromptRendererGradingTemplateTests"/> puts on production v1,
    /// applied to the v2 candidate in add_grading_prompt_v2_candidate.sql.
    ///
    /// <para>v2 ships INACTIVE, which is exactly why it needs this: an unrendered template can
    /// sit in the database for weeks with a Scriban syntax error or a leaked section in it, and
    /// the first thing to notice would be the A/B run it invalidates. The two invariant groups
    /// below are the ones that would silently void a comparison — stage isolation (if the source
    /// leaks into proofread, that pass becomes a duplicate of the evidence stage) and the nine
    /// numbered edits the SQL file's header claims to have made.</para>
    /// </summary>
    public class PromptRendererGradingTemplateV2Tests
    {
        private static string GradingTemplateV2()
        {
            var script = new EmbeddedSqlScriptSource()
                .GetScripts()
                .Single(s => s.Name == "add_grading_prompt_v2_candidate.sql")
                .Content;

            var match = Regex.Match(script, @"\$tpl\$(?<body>.*)\$tpl\$", RegexOptions.Singleline);
            Assert.True(match.Success, "could not find the $tpl$-quoted template body in the SQL script");
            return match.Groups["body"].Value;
        }

        private const string SourceSentence = "Sunburn shows that people have been in the sun.";
        private const string Translation = "\"晒斑表明着人们曾在阳光下。\"";

        private static object Model(string stage) => new
        {
            Stage = stage,
            TaskType = "A",
            SourceTitle = "Why People Get Sunburn",
            SourceText = SourceSentence,
            FlawedTranslationText = (string?)null,
            SubmissionContent = Translation,
            MeaningCheckpoints = new[]
            {
                new { Index = 1, CheckpointText = "sunburn = 晒伤，不是晒斑", Importance = "high" },
                new { Index = 2, CheckpointText = "micro-RNA 受损是信号源", Importance = "high" },
            },
            SeededErrors = Array.Empty<object>(),
            SourceSentences = new[] { new { N = 1, Text = SourceSentence } },
            CoverageNote = stage == "verdict" ? "- 原文共 1 句、约 9 词;其中 1 句被证据点到(约 100%)。" : null,
            Dimensions = new[]
            {
                new
                {
                    DimensionKey = "meaning_transfer",
                    DimensionName = "Meaning transfer",
                    PassThreshold = "Band 2 or above",
                    LevelDescriptions = new Dictionary<string, string>
                    {
                        ["1"] = "Translates the intent and consistently translates the content accurately.",
                        ["2"] = "Translates the intent and mostly translates the content accurately.",
                    },
                },
            },
            ErrorTaxonomies = new[]
            {
                new { CategoryKey = "distortion", CategoryName = "Distortion", Description = "An element of meaning is altered." },
            },
            WeakPoints = stage == "sweep"
                ? new[] { new { Name = "保留性语气词", Description = "倾向把不确定表述译得过于肯定", Recurring = true } }
                : [],
            ActiveOverrides = stage == "sweep"
                ? new[] { new { Scope = "dimension", DimensionOrRule = "meaning_transfer", RevisedRuleText = "标题误译按 major 处理" } }
                : [],
            Findings = stage == "verdict"
                ? new[]
                {
                    new
                    {
                        Id = "F1",
                        PositionRef = "标题",
                        SourceTextSnippet = "Sunburn",
                        UserTextSnippet = "晒斑",
                        ErrorCategory = "distortion",
                        DimensionKey = "meaning_transfer",
                        Severity = "major",
                        Q3WrongReading = "读者会以为信号本身被晒伤了",
                        Q2WrongReading = "读者会以为信号本身被晒伤了",
                        Summary = "核心术语方向性错误",
                        Explanation = "sunburn 指晒伤，译文用了晒斑。",
                        Suggestion = "改为「晒伤」。",
                    },
                }
                : [],
            CheckpointVerdicts = stage == "verdict"
                ? new[]
                {
                    new { Index = 1, CheckpointText = "sunburn = 晒伤，不是晒斑", Importance = "high", Verdict = "contradicted", Note = (string?)"译成了晒斑" },
                }
                : [],
        };

        [Fact]
        public void Every_stage_renders_and_no_stage_is_empty()
        {
            foreach (var stage in new[] { "evidence", "proofread", "sweep", "verdict" })
            {
                var rendered = new PromptRenderer().Render(GradingTemplateV2(), Model(stage));

                Assert.True(rendered.Trim().Length > 500, $"stage '{stage}' rendered to almost nothing");
                // An unclosed {{ if }} or a misspelt stage name renders the WHOLE template, which
                // looks fine to a smoke test and is catastrophic in production.
                Assert.DoesNotContain("{{", rendered);
            }
        }

        [Fact]
        public void Error_categories_render_the_key_and_definition_but_never_the_display_name()
        {
            // v2 change 1: "- distortion(Distortion): ..." put a second, equally plausible string
            // next to the only one ValidateFindings accepts — and it matches ORDINALLY, so
            // "Distortion" is a hard rejection and a wasted round trip.
            foreach (var stage in new[] { "evidence", "proofread", "sweep" })
            {
                var rendered = new PromptRenderer().Render(GradingTemplateV2(), Model(stage));

                Assert.Contains("- distortion:An element of meaning is altered.", rendered);
                Assert.DoesNotContain("distortion(Distortion)", rendered);
                Assert.Contains("填下表冒号左边的 key,原样照抄。", rendered);
                Assert.Contains("errorCategory 与 dimensionKey 取自两份不同的清单", rendered);
            }
        }

        [Fact]
        public void The_two_questions_are_numbered_q1_and_q2_with_no_gap_to_explain()
        {
            // v2 change 2: v1 asked q2/q3 — a gap left when q1 was removed — and then had to
            // explain the gap inside the prompt. Renumbering removes the explanation with it.
            foreach (var stage in new[] { "evidence", "proofread", "sweep" })
            {
                var rendered = new PromptRenderer().Render(GradingTemplateV2(), Model(stage));

                Assert.Contains("\"q1\"", rendered);
                Assert.Contains("\"q2\"", rendered);
                Assert.Contains("\"q2WrongReading\"", rendered);
                Assert.DoesNotContain("q3", rendered);

                // v2 change 3: the official definitions stay (they anchor what the questions
                // mean); the actionable "either true -> Major" mapping does not, because a model
                // told what the answers add up to answers backwards from the level it wants.
                Assert.Contains("Major error: An error which causes inaccuracies", rendered);
                Assert.DoesNotContain("任一为 true → 官方 Major", rendered);
                Assert.DoesNotContain("只有这两档", rendered);

                // …but the firewall the mapping used to carry survives as one line. Dropping it
                // once halved recall.
                Assert.Contains("两问都答 false", rendered);

                // The invented middle grades stay gone.
                Assert.DoesNotContain("moderate", rendered);
                Assert.DoesNotContain("critical", rendered);
            }
        }

        [Fact]
        public void No_stage_explains_itself_instead_of_instructing()
        {
            // v2 change 1: the prompt is for the model, not a reply to the reader. These are the
            // phrases v1 used to justify a rule, argue with its own earlier wording, or narrate
            // what the system does with an answer — none of them tell the model to do anything.
            foreach (var stage in new[] { "evidence", "proofread", "sweep", "verdict" })
            {
                var rendered = new PromptRenderer().Render(GradingTemplateV2(), Model(stage));

                foreach (var meta in new[]
                         {
                             "沿用历史编号", "为什么单独设这一道工序", "为什么由你单独看",
                             "上面第一条说的", "这是一个很强的声明", "这很正常",
                             "级别由系统推导", "由系统按你的两问答案自动推导", "它【不是】评分标准",
                         })
                {
                    Assert.DoesNotContain(meta, rendered);
                }
            }
        }

        [Fact]
        public void Register_is_excluded_from_style_preference_in_both_bilingual_stages()
        {
            // v2 change 4: inappropriate_register is a scoreable NAATI category that reads
            // exactly like a style preference to a model looking for a reason to skip one.
            foreach (var stage in new[] { "evidence", "sweep" })
            {
                var rendered = new PromptRenderer().Render(GradingTemplateV2(), Model(stage));
                Assert.Contains("【必记】", rendered);
                Assert.Contains("正式程度与原文体裁不符", rendered);
            }
        }

        [Fact]
        public void Evidence_stage_offers_contradicted_as_a_checkpoint_verdict()
        {
            var rendered = new PromptRenderer().Render(GradingTemplateV2(), Model("evidence"));

            // v2 change 5: hit / partial / miss had nowhere to put "the translation says the
            // OPPOSITE", which was being flattened into miss — an omission, a Minor shape — when
            // it is the canonical q3 = true.
            Assert.Contains("<hit|partial|miss|contradicted>", rendered);
            Assert.Contains("contradicted:译文就这一点说了相反或不同的事。", rendered);

            // Unchanged from v1 and still load-bearing.
            Assert.Contains("逐句核对", rendered);
            Assert.Contains($"[1] {SourceSentence}", rendered);
            Assert.Contains("Major error: An error which causes inaccuracies", rendered);
            Assert.Contains("\"q2WrongReading\"", rendered);
            Assert.DoesNotContain("Band 1: Translates the intent", rendered);
        }

        [Fact]
        public void Proofread_stage_assumes_no_genre_and_never_mentions_a_source_text()
        {
            var rendered = new PromptRenderer().Render(GradingTemplateV2(), Model("proofread"));

            // v2 change 6: v1 hard-coded 科普 and measured register against it. This bank holds
            // policy statements, public information leaflets and government notices; the genre
            // has to come from the manuscript.
            Assert.Contains("体裁由稿件自身的题材、口吻与信息密度决定,不要预设。", rendered);
            Assert.DoesNotContain("科普稿件", rendered);
            Assert.DoesNotContain("科普说明文该有的书面语", rendered);

            // v2 change 7: the stage's power is reading the Chinese as Chinese, and v1 opened by
            // telling it there is no source text — planting the very frame it wanted gone.
            Assert.DoesNotContain("原文", rendered);
            Assert.DoesNotContain("译文", rendered);

            // The prohibition that wording was carrying survives, stated in copy-editing terms.
            Assert.Contains("该说的是不是都说了", rendered);
            Assert.Contains("指代不清、修饰关系含混", rendered);
            Assert.Contains("不得使用 meaning_transfer", rendered);

            // Isolation, exactly as for v1: no English, no bands, no checklist.
            Assert.DoesNotContain(SourceSentence, rendered);
            Assert.DoesNotContain("Why People Get Sunburn", rendered);
            Assert.DoesNotContain("Band 1: Translates the intent", rendered);
            Assert.DoesNotContain("易漏检核清单", rendered);
            Assert.Contains(Translation, rendered);
            Assert.Contains("\"termUsage\"", rendered);
        }

        [Fact]
        public void Sweep_stage_keeps_the_checklist_weak_points_and_overrides_and_sees_no_findings()
        {
            var rendered = new PromptRenderer().Render(GradingTemplateV2(), Model("sweep"));

            Assert.Contains("专项排查员", rendered);
            Assert.Contains("易漏检核清单", rendered);
            Assert.Contains("清单不穷举,同类问题一样要记。", rendered);
            Assert.Contains("保留性语气词(已多次复发):倾向把不确定表述译得过于肯定", rendered);
            Assert.Contains("[dimension / meaning_transfer] 标题误译按 major 处理", rendered);

            // The checklist teaches a method; quoting the text it was tuned on would score well
            // on that text, generalise to nothing, and destroy the only instrument for telling
            // whether a change helped.
            var checklistStart = rendered.IndexOf("三、易漏检核清单", StringComparison.Ordinal);
            var checklistEnd = rendered.IndexOf("待筛材料", StringComparison.Ordinal);
            Assert.True(checklistStart >= 0 && checklistEnd > checklistStart);
            var checklist = rendered[checklistStart..checklistEnd];
            foreach (var leaked in new[] { "sunburn", "micro-RNA", "晒伤", "晒斑" })
            {
                Assert.DoesNotContain(leaked, checklist, StringComparison.OrdinalIgnoreCase);
            }

            // Independent collection, not review.
            Assert.DoesNotContain("F1 | 标题", rendered);
            Assert.DoesNotContain("Band 1: Translates the intent", rendered);
        }

        [Fact]
        public void Verdict_stage_judges_against_the_band_text_alone_and_may_reread_an_empty_dimension()
        {
            var rendered = new PromptRenderer().Render(GradingTemplateV2(), Model("verdict"));

            Assert.Contains("定档评卷员", rendered);
            Assert.Contains("Band 1: Translates the intent and consistently translates the content accurately.", rendered);
            Assert.Contains("dimension_key: meaning_transfer", rendered);
            Assert.Contains("[meaning_transfer / major] 标题 | 原文:Sunburn", rendered);
            Assert.Contains("读者会误以为:读者会以为信号本身被晒伤了", rendered);
            Assert.Contains("[1] (high) sunburn = 晒伤，不是晒斑 → contradicted / 译成了晒斑", rendered);

            // v2 change 8: the invented ratio criterion is gone. The counted facts stay, demoted
            // from criterion to background, so the stage still cannot answer "多不多" from an
            // impression — but they no longer compete with the official descriptors.
            Assert.Contains("【篇幅与证据分布】", rendered);
            Assert.Contains("评分标准只有一个", rendered);
            Assert.DoesNotContain("200 词短文", rendered);
            Assert.DoesNotContain("才是判据", rendered);
            Assert.Contains("不要换算成错误条数或百分比", rendered);

            // v2 change 9: the evidence freeze is lifted for one case only — a dimension with no
            // evidence at all — and only for that dimension.
            Assert.Contains("仅此一种情形,且仅对该维度)解除第 6 条", rendered);
            Assert.Contains("不要写成证据条目", rendered);

            // The fallback is stated against the rubric, not as a band number. The pass line is
            // Band 2 in three dimensions and Band 3 in the other two, so any single cap means a
            // different thing in each — and a number positioned relative to a pass line is
            // exactly the reasoning rule 5 forbids this stage.
            Assert.Contains("不得判 Band 1,confidence 为 low", rendered);
            Assert.DoesNotContain("最高 Band 2", rendered);
            Assert.DoesNotContain("最高只能判 Band 2", rendered);

            // The micro-rules that used to out-compete the Band text must not be in this context.
            Assert.DoesNotContain("易漏检核清单", rendered);
            Assert.DoesNotContain("主客关系与被动", rendered);
            Assert.DoesNotContain("保留性语气词", rendered);
            Assert.DoesNotContain("estimatedPassProbability", rendered);
            Assert.DoesNotContain("证据采集员", rendered);
            Assert.DoesNotContain("责任校对", rendered);
        }
    }
}
