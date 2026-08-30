-- =====================================================================
-- Design doc §2.1 node W ("对参考译文有疑问") → R: a user disputing something about
-- the reference translation reuses the EXACT SAME follow-up mechanism (node R) as a
-- grading dispute — same CreateFollowUpQuestionCommandHandler, same verdict/
-- standardRevision JSON contract, same observing→active StandardOverride path,
-- OverrideScope.translation_reference already existed in the schema/enum for this
-- (§6.8) and CreateFollowUpQuestionCommandHandler.ValidatePayload already accepted it
-- generically. The one real gap: the shared_methodology/followup row from
-- add_followup_prompt_template.sql never included the reference translation's own
-- text, so the AI had no way to actually answer a question about it — a follow-up
-- literally asking "why does the reference translation say X" got a response from a
-- model that had never been shown what X was.
--
-- CreateFollowUpQuestionCommandHandler now additionally supplies `reference_translation`
-- (null if Step 7's deep-learning content hasn't been generated for this Question yet —
-- the template guards on this with {{ if reference_translation }}, confirmed safe against
-- a null nested object by PromptRendererReferenceTranslationRenderingTests before this
-- row was written) with `reference_text`/`comparison_notes`.
--
-- This is a version=2 replacement of the existing shared_methodology/followup row, not a
-- second additive row — same "new version, deactivate old" precedent as
-- fix_followup_exam_specific_prompt_template.sql, chosen over adding a second row so all
-- of this handler's content-injection logic (and the one JSON output contract) stays in a
-- single template rather than split across two rows whose relative order would need to be
-- reasoned about. The original row (add_followup_prompt_template.sql) was inserted without
-- an explicit id literal (unlike the exam_specific row that fix file deactivated by id), so
-- it's matched here by (template_type, layer, version) instead — there should only ever be
-- one such row.
--
-- Everything from the original v1 row is preserved (question/context, grading materials,
-- rubric/taxonomy lists, the 8/9-category grading self-review checklist, the JSON output
-- contract) with two additions: (1) a 【参考译文】section rendered only when
-- reference_translation is present, and (2) a short addendum to 重要说明 covering how to
-- judge a reference-translation dispute — translation has no single correct answer, so the
-- user proposing an equally-valid alternative phrasing does NOT by itself mean the
-- reference translation is wrong; only a genuine issue (mistranslation, omission, unnatural
-- phrasing) in the reference text itself warrants a translation_reference-scoped
-- standardRevision. That note is only ever recorded as an audit-chain correction note
-- (design doc §10.6) — it does NOT automatically rewrite reference_translations.reference_text,
-- consistent with how a grading_rubric-scoped override never rewrites assessment_dimensions
-- either.
--
-- If follow-up answers ever start failing to parse, or a reference-translation dispute
-- comes back saying the AI wasn't shown the reference text, check this row (prompt_templates
-- where template_type='followup' AND layer='shared_methodology') hasn't been deactivated or
-- superseded incompatibly.
-- =====================================================================

BEGIN;

UPDATE prompt_templates
SET is_active = FALSE
WHERE template_type = 'followup' AND layer = 'shared_methodology' AND version = 1 AND is_active = TRUE;

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    NULL,
    'translation',
    'followup',
    'shared_methodology',
    $tpl$
【用户的追问】
{{ question_text }}
{{ if context_ref }}
(用户所指的具体上下文: {{ context_ref }})
{{ end }}

【本题材料】
任务类型: {{ task_type }}
原文:
{{ source_text }}

用户提交内容(JSON):
{{ submission_content }}

{{ if grading_results.size > 0 }}
已有评分结果:
{{ for r in grading_results }}
- 维度[{{ r.dimension_key }}] Band {{ r.band }}: {{ r.rationale }}
{{ end }}
{{ end }}

{{ if errors.size > 0 }}
已有错误清单:
{{ for e in errors }}
- 位置[{{ e.position_ref }}] 类别:{{ e.error_category }} 是否影响核心信息:{{ e.impacts_core }} 说明:{{ e.explanation }}
{{ end }}
{{ end }}

{{ if reference_translation }}
【参考译文】(仅供参考,不是唯一正确答案;如果用户的追问是针对参考译文本身提出异议,请依据这份文本回答)
{{ reference_translation.reference_text }}
{{ if reference_translation.comparison_notes }}
参考译文附带的技巧/易错点笔记:{{ reference_translation.comparison_notes }}
{{ end }}
{{ end }}

本考试类型的评分维度(仅当你认为需要为某个维度的判断方式补充一条修正说明时,dimensionOrRule必须从中选取dimension_key):
{{ for d in dimensions }}
- {{ d.dimension_key }}: {{ d.dimension_name }}
{{ end }}

本考试类型的错误分类(供你判断用户的争议是否涉及错误归类):
{{ for t in error_taxonomies }}
- {{ t.category_key }}: {{ t.category_name }}
{{ end }}

【任务】
针对用户的追问,判断用户的观点是否成立,并给出解答。

重要说明:官方评分标准(上方评分维度的Band描述)本身是准确、权威、不可更改的依据,你的解答绝不是去质疑、
修正或覆盖它——standardRevision字段记录的从来都不是"重写官方rubric原文",而是给AI自己积累一条评判补丁。
复核这次判断时,请对照以下几类AI自身可能出现的疏漏逐一排查真正的病因(而不是简单地看用户说得有没有道理):
- 漏判:用户译文中实际存在的错误/细节,AI没有发现
- 误判(false positive):用户译文其实站得住脚,AI却当成了错误扣分——尤其警惕把自己"更偏好"的某种措辞
  当成了隐形标准答案,排斥掉其他同样合规的译法
- 原文理解偏差:AI对原文本身的理解/翻译不合理,导致评判依据从一开始就是错的
- 错误分类归错类:确实发现了错误,但8类错误类型判错了(如把"表达不地道"归成了"扭曲")
- 维度归错:错误真实存在,但算到了错的评分维度头上——不同维度的Band描述和通过线完全不同,算错维度会
  导致一个维度虚高、另一个虚低
- Band档位判错:错误类型和维度都对,但严重程度判断偏了(如把影响核心信息的错误按过轻的Band判,或反之)
- 累积密度算漏:单条错误都不严重,但多条叠加已构成显著影响,只顾逐条打分而没有做整体复核
- 前后判不一致:同类错误这次判得严、下次判得宽,没有外部原因驱动——这个信号本身值得记录,但病因是判断
  不稳定,不是某条具体逻辑判错
{{ if task_type == "B" }}
- (本题为TaskB)位置定位错:把用户标注的位置错误地匹配到了另一个预设错误上,导致误判用户"找错了/漏找了"
{{ end }}
{{ if reference_translation }}
- (针对参考译文的异议)翻译没有唯一正确答案:如果用户提出的是一种同样合理的替代译法,不代表参考译文
  错了,verdict可以是user_correct(用户的译法也站得住脚),但不必然需要standardRevision;只有参考译文
  本身确实存在问题(误译、遗漏、生硬翻译腔、不符合原文语气等)时,才需要以
  scope="translation_reference"记录一条correction note——这条note只是留痕供后续人工复核,不会自动
  改写reference_translations表中的文本
{{ end }}
这类"AI评判本身的疏漏"应当被记录积累下来,作为以后遇到同类情况时AI应参考的补充提示,而官方rubric原文本身不受任何影响。

【输出格式】
严格只输出以下JSON,不要输出markdown代码块围栏之外的任何文字:
{
  "aiResponse": "<对用户疑问的解答说明>",
  "verdict": "user_correct" 或 "user_incorrect" 或 "partial",
  "standardRevision": null,
  说明: 仅当verdict为"user_correct"、且这次误判确实源于上方列出的某一类"AI评判本身的疏漏"(而不是用户
  单纯运气好蒙对/该题本来就有争议空间)时,才将standardRevision替换为以下对象——它记录的是AI以后应如何
  修正自己的判断方式,不是修改官方评分标准或参考译文原文本身(否则保持null,不要仅因为解答了用户疑问就
  填写此项):
  {
    "scope": "grading_rubric"(疏漏与某个评分维度的判断/应用方式有关) 或 "translation_reference"(疏漏与原文理解、翻译参考或参考译文本身有关),
    "dimensionOrRule": "<grading_rubric时必须是上方评分维度列表中的dimension_key之一,标明哪个维度的判断方式需要补充说明;translation_reference时为该类问题的简短标识>",
    "originalRuleText": "<AI这次实际做出的错误判断或疏漏具体是什么,建议注明属于上述哪一类(漏判/误判/原文理解偏差/错误分类归错类/维度归错/Band档位判错/累积密度算漏/前后判不一致/位置定位错/参考译文本身的问题),没有明确描述则为null>",
    "revisedRuleText": "<以后遇到同类情况,AI应该如何正确判断——这是供后续评判参考的补充说明,不是官方rubric或参考译文的新文本>"
  }
}
$tpl$,
    2,
    TRUE
);

COMMIT;
