-- =====================================================================
-- Follow-up threads (design decision, 2026-09-02): a dispute is no longer answered in one
-- shot — CreateFollowUpThreadCommandHandler/AddFollowUpMessageCommandHandler now run one AI
-- call per round of an open FollowUpThread, and only a SEPARATE closing "summary" call
-- (AiOperationType.followup_summary, see add_followup_summary_prompt_template.sql) decides
-- the final verdict / StandardOverride / submission outcome. Two consequences for this
-- shared_methodology/followup row:
--
-- 1. It needs to see the thread's prior rounds, or a round-2+ reply has no idea what was
--    already asked/answered — FollowUpThreadSupport.BuildTemplateModel now always supplies
--    `history` (empty array for round 1) alongside the existing `question_text` (still just
--    the NEWEST message, same variable name/meaning as before).
-- 2. A per-round reply must stop proposing standardRevision — with multiple rounds per
--    dispute, letting every round independently decide "this deserves a rubric correction
--    note" risked several StandardOverride rows being created for what is really one dispute
--    (CountDistinctQuestionsPendingAsync counts by distinct Question id, so it wouldn't over-
--    count the activation threshold, but it would still leave duplicate/inconsistent
--    observing rows behind). Only the closing summary call may propose one now.
--
-- Version=3 replacement of the existing shared_methodology/followup row (version=2, from
-- add_followup_reference_translation_content.sql) — same "new version, deactivate old"
-- precedent as that file and fix_followup_exam_specific_prompt_template.sql. Matched by
-- (template_type, layer, version) since this row has never been inserted with an explicit id
-- literal.
--
-- Everything else from v2 is unchanged: question/context, grading materials, the reference
-- translation section, rubric/taxonomy lists, and the 8/9-category self-review checklist
-- (still useful for judging a single round's verdict, even though that verdict is now
-- informational only — see FollowUpMessage.Verdict's doc comment). The exam_specific/followup
-- row (fix_followup_exam_specific_prompt_template.sql, still version=2/active, unchanged by
-- this migration) concatenates after this one and needs no changes — its NAATI-CT-specific
-- reminders apply just as well to a single round.
--
-- If a round-2+ follow-up reply reads as if it has no memory of the earlier turns, or a
-- per-round reply still comes back with a non-null standardRevision, check this row
-- (prompt_templates where template_type='followup' AND layer='shared_methodology') hasn't
-- been deactivated or superseded incompatibly.
-- =====================================================================

BEGIN;

UPDATE prompt_templates
SET is_active = FALSE
WHERE template_type = 'followup' AND layer = 'shared_methodology' AND version = 2 AND is_active = TRUE;

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    NULL,
    'translation',
    'followup',
    'shared_methodology',
    $tpl$
{{ if history.size > 0 }}
【本线程此前的对话】(用户仍在同一次追问中继续提问,请结合以下已有的问答历史作答,不要重复已经说过的内容,也不要前后自相矛盾;如果这一轮用户是在反驳你之前的回答,请重新审视你的立场,该改口就改口,不要为了保持一致而回避明显的错误)
{{ for m in history }}
{{ if m.role == "user" }}用户: {{ else }}AI: {{ end }}{{ m.content }}
{{ end }}
{{ end }}

【用户的{{ if history.size > 0 }}最新{{ end }}追问】
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

本考试类型的评分维度(供你判断用户的争议是否涉及哪个维度):
{{ for d in dimensions }}
- {{ d.dimension_key }}: {{ d.dimension_name }}
{{ end }}

本考试类型的错误分类(供你判断用户的争议是否涉及错误归类):
{{ for t in error_taxonomies }}
- {{ t.category_key }}: {{ t.category_name }}
{{ end }}

【任务】
针对用户{{ if history.size > 0 }}最新{{ end }}的追问,判断用户的观点是否成立,并给出解答。这只是一轮对话中的
一次回复,用户可能会继续追问——你的verdict只是这一轮的看法,不会单独触发任何评分标准的修正记录,后续会有
另一次独立的"总结复核"在用户结束这次追问时,综合整个对话给出最终结论。

重要说明:官方评分标准(上方评分维度的Band描述)本身是准确、权威、不可更改的依据,你的解答绝不是去质疑、
修正或覆盖它。复核这次判断时,请对照以下几类AI自身可能出现的疏漏逐一排查真正的病因(而不是简单地看用户说
得有没有道理):
- 漏判:用户译文中实际存在的错误/细节,AI没有发现
- 误判(false positive):用户译文其实站得住脚,AI却当成了错误扣分——尤其警惕把自己"更偏好"的某种措辞
  当成了隐形标准答案,排斥掉其他同样合规的译法
- 原文理解偏差:AI对原文本身的理解/翻译不合理,导致评判依据从一开始就是错的
- 错误分类归错类:确实发现了错误,但8类错误类型判错了(如把"表达不地道"归成了"扭曲")
- 维度归错:错误真实存在,但算到了错的评分维度头上——不同维度的Band描述和通过线完全不同,算错维度会
  导致一个维度虚高、另一个虚低
- Band档位判错:错误类型和维度都对,但严重程度判断偏了(如把影响核心信息的错误按过轻的Band判,或反之)
- 累积密度算漏:单条错误都不严重,但多条叠加已构成显著影响,只顾逐条打分而没有做整体复核
- 前后判不一致:同类错误这次判得严、下次判得宽,没有外部原因驱动
{{ if task_type == "B" }}
- (本题为TaskB)位置定位错:把用户标注的位置错误地匹配到了另一个预设错误上,导致误判用户"找错了/漏找了"
{{ end }}
{{ if reference_translation }}
- (针对参考译文的异议)翻译没有唯一正确答案:如果用户提出的是一种同样合理的替代译法,不代表参考译文错了,
  verdict可以是user_correct(用户的译法也站得住脚)
{{ end }}

【输出格式】
严格只输出以下JSON,不要输出markdown代码块围栏之外的任何文字:
{
  "aiResponse": "<对用户这一轮追问的解答说明>",
  "verdict": "user_correct" 或 "user_incorrect" 或 "partial"
}
$tpl$,
    3,
    TRUE
);

COMMIT;
