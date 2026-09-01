-- =====================================================================
-- Follow-up threads (design decision, 2026-09-02, see add_followup_thread_history_prompt_
-- template.sql for the fuller rationale): CloseFollowUpThreadCommandHandler is the one call
-- in the whole multi-round conversation allowed to produce a verdict with real side effects
-- (StandardOverride creation, submission Graded/StandardRevised outcome) — every per-round
-- reply up to this point was purely conversational. New template_type='followup_summary' row
-- (no prior version to replace — this operation type didn't exist before) so the two
-- contracts can never be confused: 'followup' rows never emit standardRevision any more,
-- only this one does.
--
-- Unlike the per-round template, this one has no single distinguished "newest question" —
-- CloseFollowUpThreadCommandHandler passes questionText="" (always empty, never rendered
-- here) and history = the ENTIRE thread's messages, oldest first. The JSON output contract
-- reuses the exact standardRevision shape and 8/9-category self-review checklist the old
-- single-shot CreateFollowUpQuestionCommandHandler used (add_followup_reference_translation_
-- content.sql) — that reasoning doesn't change just because the judgment now happens once at
-- the end of a conversation instead of once per question.
-- =====================================================================

BEGIN;

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    NULL,
    'translation',
    'followup_summary',
    'shared_methodology',
    $tpl$
【用户与AI的完整追问对话】(用户对某条判定有异议,发起了这次追问;以下是双方从头到尾的完整往来,请通读全部
内容再下结论,不要只看最后一轮)
{{ for m in history }}
{{ if m.role == "user" }}用户: {{ else }}AI: {{ end }}{{ m.content }}
{{ end }}
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
【参考译文】(仅供参考,不是唯一正确答案;如果这次追问是针对参考译文本身提出异议,请依据这份文本判断)
{{ reference_translation.reference_text }}
{{ if reference_translation.comparison_notes }}
参考译文附带的技巧/易错点笔记:{{ reference_translation.comparison_notes }}
{{ end }}
{{ end }}

本考试类型的评分维度(仅当你认为需要为某个维度的判断方式补充一条修正说明时,dimensionOrRule必须从中选取dimension_key):
{{ for d in dimensions }}
- {{ d.dimension_key }}: {{ d.dimension_name }}
{{ end }}

本考试类型的错误分类(供你判断这次争议是否涉及错误归类):
{{ for t in error_taxonomies }}
- {{ t.category_key }}: {{ t.category_name }}
{{ end }}

【任务】
用户即将结束这次追问,现在需要你综合上面完整的对话给出最终结论——用户的观点整体上是否成立。这是这次追问
的唯一权威判定,会据此更新提交的状态,不再有下一轮机会,请慎重。

重要说明:官方评分标准(上方评分维度的Band描述)本身是准确、权威、不可更改的依据,你的解答绝不是去质疑、
修正或覆盖它——standardRevision字段记录的从来都不是"重写官方rubric原文",而是给AI自己积累一条评判补丁。
下结论时,请对照以下几类AI自身可能出现的疏漏逐一排查真正的病因(而不是简单地看用户说得有没有道理,也不要
仅仅因为AI在对话中途曾经让步或改口就自动判user_correct——请重新独立评估整个对话反映出的问题实质):
- 漏判:用户译文中实际存在的错误/细节,AI没有发现
- 误判(false positive):用户译文其实站得住脚,AI却当成了错误扣分——尤其警惕把自己"更偏好"的某种措辞
  当成了隐形标准答案,排斥掉其他同样合规的译法
- 原文理解偏差:AI对原文本身的理解/翻译不合理,导致评判依据从一开始就是错的
- 错误分类归错类:确实发现了错误,但8类错误类型判错了(如把"表达不地道"归成了"扭曲")
- 维度归错:错误真实存在,但算到了错的评分维度头上——不同维度的Band描述和通过线完全不同,算错维度会
  导致一个维度虚高、另一个虚低
- Band档位判错:错误类型和维度都对,但严重程度判断偏了(如把影响核心信息的错误按过轻的Band判,或反之)
- 累积密度算漏:单条错误都不严重,但多条叠加已构成显著影响,只顾逐条打分而没有做整体复核
- 前后判不一致:对话中AI自己前后判得不一致,没有外部原因驱动——这个信号本身值得记录,但病因是判断不
  稳定,不是某条具体逻辑判错
{{ if task_type == "B" }}
- (本题为TaskB)位置定位错:把用户标注的位置错误地匹配到了另一个预设错误上,导致误判用户"找错了/漏找了"
{{ end }}
{{ if reference_translation }}
- (针对参考译文的异议)翻译没有唯一正确答案:如果用户提出的是一种同样合理的替代译法,不代表参考译文
  错了,finalVerdict可以是user_correct(用户的译法也站得住脚),但不必然需要standardRevision;只有参考译文
  本身确实存在问题(误译、遗漏、生硬翻译腔、不符合原文语气等)时,才需要以
  scope="translation_reference"记录一条correction note——这条note只是留痕供后续人工复核,不会自动
  改写reference_translations表中的文本
{{ end }}
这类"AI评判本身的疏漏"应当被记录积累下来,作为以后遇到同类情况时AI应参考的补充提示,而官方rubric原文本身不受任何影响。

【输出格式】
严格只输出以下JSON,不要输出markdown代码块围栏之外的任何文字:
{
  "aiResponse": "<对这次追问的最终结论说明,可以简要回顾整个对话>",
  "finalVerdict": "user_correct" 或 "user_incorrect" 或 "partial",
  "standardRevision": null,
  说明: 仅当finalVerdict为"user_correct"、且这次误判确实源于上方列出的某一类"AI评判本身的疏漏"(而不是用户
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
    1,
    TRUE
);

COMMIT;
