-- =====================================================================
-- Step 5 (追问与Rubric校准): same recurring gap as Step 3's question_gen row and
-- Step 4's grading row — no prompt_templates row of template_type='followup' has ever
-- been seeded, because CreateFollowUpQuestionCommandHandler didn't exist until now.
--
-- This is shared_methodology/translation (not exam_specific) because the content-injection
-- variables and JSON contract are generic to any translation-shaped follow-up dispute, not
-- NAATI-CT-specific — consistent with how the grading/question_gen shared_methodology rows
-- are scoped.
--
-- CreateFollowUpQuestionCommandHandler supplies: question_text, context_ref, task_type,
-- source_text, submission_content, grading_results[] (dimension_key/band/rationale),
-- errors[] (position_ref/error_category/explanation/impacts_core), dimensions[]
-- (dimension_key/dimension_name), error_taxonomies[] (category_key/category_name) — see
-- CreateFollowUpQuestionCommandHandler.BuildTemplateModel.
--
-- Corrected 2026-08-30 (user feedback, same day as the initial version): standardRevision is
-- NOT a mechanism for rewriting the official rubric text (assessment_dimensions.level_descriptions
-- stays authoritative and untouched) — it's a growing corpus of AI-judgment correction notes,
-- patching how the AI applies the standard in specific recurring situations. original/revised
-- rule text hold "what the AI's flawed judgment was" / "what it should conclude next time in a
-- similar situation" — not before/after rubric wording.
--
-- Expanded same day (further user feedback) into a 7/8-category self-review checklist the AI is
-- asked to check itself against rather than just the original 2 examples: missed error, false
-- positive (flagging a correct translation as wrong — often because the AI's own internally
-- generated "ideal phrasing" quietly acts as an invisible answer key, the same reference-answer
-- contamination risk design doc §10.2 already guards against for reference_translations, just
-- harder to spot since it never touches that table), source-text misreading, error miscategorized
-- (right that it's wrong, wrong about which of the 8 taxonomy categories), wrong dimension
-- attributed (worse than miscategorizing the error type, since each dimension has its own Band
-- text/pass threshold — misattribution inflates one dimension and deflates another), Band
-- severity miscalibrated (design doc's own "most subjective step" — Band boundary text has
-- inherent judgment room), cumulative density missed (§5's "individually-minor-but-taken-together"
-- principle — missed relationships between errors, not a missed error itself), inconsistent
-- repeat judgment (no external cause, the AI is just unstable call to call — worth recording as a
-- signal even though there's no "correct rule" to state), and (TaskB only) position mismatch
-- (matching the user's annotation to the wrong seeded error — mechanical, not a judgment error,
-- but still produces a wrong "found it / missed it" verdict). See the template body's 重要说明
-- block for the exact (condensed) wording given to the model.
--
-- If follow-up answers ever start failing to parse, check this row (prompt_templates where
-- template_type='followup' AND layer='shared_methodology') hasn't been deactivated or
-- superseded incompatibly (same caveat as the question_gen/grading rows from Steps 3-4).
-- =====================================================================

BEGIN;

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
    "scope": "grading_rubric"(疏漏与某个评分维度的判断/应用方式有关) 或 "translation_reference"(疏漏与原文理解或翻译参考有关),
    "dimensionOrRule": "<grading_rubric时必须是上方评分维度列表中的dimension_key之一,标明哪个维度的判断方式需要补充说明;translation_reference时为该类原文理解问题的简短标识>",
    "originalRuleText": "<AI这次实际做出的错误判断或疏漏具体是什么,建议注明属于上述哪一类(漏判/误判/原文理解偏差/错误分类归错类/维度归错/Band档位判错/累积密度算漏/前后判不一致/位置定位错),没有明确描述则为null>",
    "revisedRuleText": "<以后遇到同类情况,AI应该如何正确判断——这是供后续评判参考的补充说明,不是官方rubric的新文本>"
  }
}
$tpl$,
    1,
    TRUE
);

COMMIT;
