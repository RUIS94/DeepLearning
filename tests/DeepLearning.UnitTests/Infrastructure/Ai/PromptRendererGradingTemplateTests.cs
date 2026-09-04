using System.Text.RegularExpressions;
using DeepLearning.Infrastructure.Ai;
using DeepLearning.Infrastructure.Persistence.Sql;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    /// <summary>
    /// Renders the REAL grading template shipped in rebuild_grading_prompt_v10_official_two_levels.sql
    /// (read out of the embedded SQL script, so a hand edit to that file is covered) through the
    /// same PromptRenderer.Render call GradeSubmissionCommandHandler uses.
    ///
    /// The whole point of splitting the call is that each stage sees only its own material. If
    /// the official Band descriptions leak into a collection stage, or the detection checklist
    /// leaks into the verdict stage, or the proofread stage is shown the English source, the
    /// reason for splitting is gone and nothing else in the system would notice. These tests are
    /// the guard on that isolation.
    /// </summary>
    public class PromptRendererGradingTemplateTests
    {
        private static string GradingTemplate()
        {
            var script = new EmbeddedSqlScriptSource()
                .GetScripts()
                .Single(s => s.Name == "rebuild_grading_prompt_v10_official_two_levels.sql")
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
                new { Index = 3, CheckpointText = "炎症反应本身是正常保护机制", Importance = "medium" },
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
                        Summary = "核心术语方向性错误",
                        Explanation = "sunburn 指晒伤，译文用了晒斑。",
                        Suggestion = "改为「晒伤」。",
                    },
                }
                : [],
            CheckpointVerdicts = stage == "verdict"
                ? new[]
                {
                    new { Index = 1, CheckpointText = "sunburn = 晒伤，不是晒斑", Importance = "high", Verdict = "miss", Note = (string?)"译成了晒斑" },
                }
                : [],
        };

        [Fact]
        public void Evidence_stage_asks_the_three_questions_and_forces_per_sentence_coverage()
        {
            var rendered = new PromptRenderer().Render(GradingTemplate(), Model("evidence"));

            Assert.Contains("证据采集员", rendered);
            Assert.Contains("Major error: An error which causes inaccuracies", rendered);
            // The source is split and numbered by SplitSentences, not by the model — otherwise a
            // model that stopped early could emit fewer rows and still look complete.
            Assert.Contains("强制逐句核对", rendered);
            Assert.Contains("你【不需要也不要】自己切句", rendered);
            Assert.Contains($"[1] {SourceSentence}", rendered);
            Assert.Contains("\"sentences\"", rendered);
            Assert.Contains("- distortion(Distortion):An element of meaning is altered.", rendered);

            // Checkpoint numbering is the anchor BuildCheckpointVerdicts pairs the answers back
            // onto, so it must come out 1, 2, 3 — not blank, and not all "1".
            Assert.Contains("[1] (high) sunburn = 晒伤，不是晒斑", rendered);
            Assert.Contains("[2] (high) micro-RNA 受损是信号源", rendered);
            Assert.Contains("[3] (medium) 炎症反应本身是正常保护机制", rendered);

            // The model must ask for booleans, never for a level name — that collision is what
            // let the audit stage write "minor" while its own reasoning said otherwise.
            Assert.Contains("\"q2\"", rendered);
            Assert.Contains("\"q3\"", rendered);
            // Two questions, two levels — the invented middle grades are gone entirely.
            Assert.DoesNotContain("moderate", rendered);
            Assert.DoesNotContain("critical", rendered);
            Assert.DoesNotContain("scopeBeyondSentence", rendered);

            // q3 has to be paid for: naming the wrong reading is the difference between a
            // comprehension failure and prose that is merely clumsy. Without this the first v3
            // run answered q3 = true for almost everything and came back 38 major / 8 minor.
            Assert.Contains("\"q3WrongReading\"", rendered);

            // q3 must ask about the RESULT only. When it listed the same content categories as
            // q1 (指称对象 / 逻辑关系 / 范围 …), every precision loss read as a comprehension
            // failure and a dropped article came back as a major error.
            Assert.Contains("精度受损不等于理解出错", rendered);
            Assert.Contains("官方 Major", rendered);

            // …and that severity guidance must not be read as recording guidance. Saying "most
            // findings are not severe" once halved recall: the stage marked 10 of 13 sentences ok
            // and reported two findings.
            Assert.Contains("两问只决定这一处是 Major 还是 Minor,不决定记不记", rendered);
            Assert.Contains("同样必须原样记进 findings[]", rendered);

            // "ok" is a strong claim, not the default.
            Assert.Contains("这是一个很强的声明", rendered);
            Assert.Contains("拿不准就填 deviation", rendered);
            Assert.DoesNotContain("拿不到,或拿到的是错的——指称对象", rendered);

            Assert.DoesNotContain("Band 1: Translates the intent", rendered);
            Assert.DoesNotContain("定档评卷员", rendered);
        }

        [Fact]
        public void Evidence_stage_tells_the_model_to_return_no_checkpoints_when_there_are_none()
        {
            // A silently absent section is what the model filled in by inventing thirteen
            // checkpoint verdicts. Absent must be stated, not merely absent.
            var model = new
            {
                Stage = "evidence",
                TaskType = "A",
                SourceTitle = (string?)null,
                SourceText = SourceSentence,
                FlawedTranslationText = (string?)null,
                SubmissionContent = Translation,
                MeaningCheckpoints = Array.Empty<object>(),
                SeededErrors = Array.Empty<object>(),
                SourceSentences = new[] { new { N = 1, Text = SourceSentence } },
                CoverageNote = (string?)null,
                Dimensions = Array.Empty<object>(),
                ErrorTaxonomies = Array.Empty<object>(),
                WeakPoints = Array.Empty<object>(),
                ActiveOverrides = Array.Empty<object>(),
                Findings = Array.Empty<object>(),
                CheckpointVerdicts = Array.Empty<object>(),
            };

            var rendered = new PromptRenderer().Render(GradingTemplate(), model);

            Assert.Contains("本题没有预设信息点", rendered);
            Assert.Contains("不得凭空编造信息点", rendered);
            // A null title must not render an empty "原文标题" heading, which reads to the grader
            // as "the source had a blank title".
            Assert.DoesNotContain("原文标题", rendered);
        }

        [Fact]
        public void Proofread_stage_never_sees_the_source_text()
        {
            var rendered = new PromptRenderer().Render(GradingTemplate(), Model("proofread"));

            Assert.Contains("责任校对", rendered);
            Assert.Contains("术语一致性", rendered);
            Assert.Contains("\"termUsage\"", rendered);
            Assert.Contains(Translation, rendered);

            // The bait that cost a whole grading run: a literal dimension key sitting in the
            // JSON skeleton one field away from an errorCategory placeholder that was only an
            // abstract description. The model copied the only concrete string it could see into
            // the nearest slot, three attempts running.
            Assert.DoesNotContain("<textual_norms|language_proficiency>", rendered);
            Assert.Contains("errorCategory 与 dimensionKey 是两个不同的字段", rendered);

            // The entire value of this pass is that the English is not there to paper over the
            // Chinese. If the source ever leaks in, the pass silently becomes a duplicate of the
            // evidence stage.
            Assert.DoesNotContain(SourceSentence, rendered);
            Assert.DoesNotContain("Why People Get Sunburn", rendered);
            Assert.DoesNotContain("原文正文", rendered);
            Assert.DoesNotContain("Band 1: Translates the intent", rendered);

            // meaning_transfer is named only to forbid it: this pass has no source to check
            // faithfulness against, so it must not route findings there. (It also appears in the
            // v5 warning that errorCategory and dimensionKey draw on disjoint lists — hence two
            // mentions, both prohibitions, and never as an offered value.)
            Assert.Contains("不要往 meaning_transfer 上挂", rendered);

            // Completeness needs the source and is out of scope; an ambiguous modifier does not
            // and is exactly what this pass is for.
            Assert.Contains("完整性判断一律不属于你", rendered);
            Assert.Contains("指代不清、修饰关系含混", rendered);
            Assert.Equal(2, Regex.Matches(rendered, "meaning_transfer").Count);
        }

        [Fact]
        public void Sweep_stage_carries_the_checklist_weak_points_and_overrides_but_no_earlier_findings()
        {
            var rendered = new PromptRenderer().Render(GradingTemplate(), Model("sweep"));

            Assert.Contains("专项排查员", rendered);
            Assert.Contains("易漏检核清单", rendered);
            Assert.Contains("主客关系与被动", rendered);

            // The list is a way of looking, not a word list to match — and the stage is not told
            // about colleagues it cannot see and cannot act on.
            Assert.Contains("不是穷举清单", rendered);
            Assert.Contains("清单没写到的同类问题,同样要记", rendered);
            Assert.DoesNotContain("另有评卷员", rendered);
            Assert.DoesNotContain("复筛员", rendered);

            // The checklist must teach a method, not recite one article's answers. Quoting the
            // text it was tuned on would score well on that text and generalise to nothing —
            // and would destroy the only instrument for telling whether a change helped.
            //
            // Scoped to the checklist section: the same words legitimately appear further down,
            // where the article actually under grading is quoted.
            var checklistStart = rendered.IndexOf("三、易漏检核清单", StringComparison.Ordinal);
            var checklistEnd = rendered.IndexOf("待筛材料", StringComparison.Ordinal);
            Assert.True(checklistStart >= 0 && checklistEnd > checklistStart);
            var checklist = rendered[checklistStart..checklistEnd];

            foreach (var leaked in new[]
                     {
                         "released as a signal", "marker for injury", "radiation",
                         "sunburn", "psoriasis", "micro-RNA", "晒伤", "晒斑",
                     })
            {
                Assert.DoesNotContain(leaked, checklist, StringComparison.OrdinalIgnoreCase);
            }
            Assert.Contains("保留性语气词(已多次复发):倾向把不确定表述译得过于肯定", rendered);
            Assert.Contains("[dimension / meaning_transfer] 标题误译按 major 处理", rendered);

            // Independent collection, not review: being shown a list turns the model into a
            // validator that adds nothing. This is the v2 failure this stage exists to undo.
            Assert.DoesNotContain("F1 | 标题", rendered);
            Assert.DoesNotContain("上一阶段", rendered);
            Assert.DoesNotContain("Band 1: Translates the intent", rendered);
        }

        [Fact]
        public void Verdict_stage_renders_the_official_bands_and_merged_evidence_but_no_checklist()
        {
            var rendered = new PromptRenderer().Render(GradingTemplate(), Model("verdict"));

            Assert.Contains("定档评卷员", rendered);
            Assert.Contains("Band 1: Translates the intent and consistently translates the content accurately.", rendered);
            Assert.Contains("dimension_key: meaning_transfer", rendered);
            Assert.Contains("[meaning_transfer / major] 标题 | 原文:Sunburn", rendered);
            Assert.Contains("[1] (high) sunburn = 晒伤，不是晒斑 → miss / 译成了晒斑", rendered);
            Assert.Contains("\"alternativeBand\"", rendered);
            Assert.Contains("\"confidence\"", rendered);

            // Empty evidence must not read as a perfect performance — that is how textual_norms
            // came out at Band 1 with the rationale "没有证据显示文本规范方面的问题".
            Assert.Contains("证据为空 ≠ 表现完美", rendered);

            // "Taken together" now judges against counted facts rather than an impression of how
            // long the text felt — the step that waved off five errors in a 250-word text.
            // The evidence line carries the reader's mistaken reading, so 2a can check the claim
            // instead of trusting a severity label derived somewhere it cannot see.
            Assert.Contains("读者会误以为:读者会以为信号本身被晒伤了", rendered);

            Assert.Contains("【篇幅与证据分布】", rendered);
            Assert.Contains("其中 1 句被证据点到", rendered);
            Assert.Contains("不要凭印象估", rendered);
            Assert.Contains("最高只能判 Band 2", rendered);

            // The source and translation stay, so proportion words like "mostly" can be judged —
            // but only under an explicit, bounded licence.
            Assert.Contains("只有两个被许可的用途", rendered);
            Assert.Contains("不得据此新增证据条目", rendered);

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
