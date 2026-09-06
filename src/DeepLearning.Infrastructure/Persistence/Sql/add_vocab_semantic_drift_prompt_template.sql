-- =====================================================================
-- 2026-09-06: vocab_semantic_drift 提示词 v1(NAATI CT 英译中专属)。
--
-- 背景:深入学习生成的四部分材料里,每题的 vocab 逐行冻结存 vocab_expressions;跨题的
-- 「这个词到目前为止有哪些义 / 用法」由 vocab_glossary 一行累积,AI 维护。
-- 词首次出现 → 后端确定性写种子(不调 AI)。此后每次在新题里复现 → AnalyzeVocabSemanticDriftJob
-- 批量调这个提示词:逐条判断本篇这个词的意思相对 known_semantics 有没有已记录之外的新义,
-- 有才给出并入新义后的完整文本。义相同(哪怕措辞不同)一律 changed=false,glossary 不动。
--
-- 隔离:只拿 task_type + source_text + 每个词条的历史笔记,绝不接触任何一份 submission /
-- 评分结果 —— 与 deep_learning 生成同一条隔离线。
--
-- model 字段(VocabSemanticDriftService 提供):
--   task_type   : "A" | "B"
--   source_text : 本题原文全文
--   items[]     : english_expr / this_chinese(本题该词的推荐译法)/ this_note(本题该词的语境笔记)
--                 / known_semantics(vocab_glossary 里已累积的语义文本)
--
-- 手动执行(Supabase)。幂等:DELETE + INSERT。
-- =====================================================================

BEGIN;

DELETE FROM prompt_templates
WHERE template_type = 'vocab_semantic_drift';

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    NULL,
    'vocab_semantic_drift',
    'exam_specific',
    $tpl$
你在维护一份英译中学习者的「词汇累积语义」记录。下面每个词条都带有:它到目前为止已记录的语义说明(known_semantics),以及它在本篇原文里这一次的推荐译法和语境笔记。请逐条判断:本篇里这个词的意思 / 用法 / 语域,相对 known_semantics,有没有出现「已记录之外」的新义、新用法、新搭配或新语域。

判定标准:
- 只是换了说法、举了新例子、措辞更细,但义项本质没超出 known_semantics 覆盖范围 → changed = false。
- 本篇确实用到了 known_semantics 没有涵盖的一个义项 / 引申义 / 专业用法 / 语体色彩 → changed = true。
- known_semantics 为空或极简,而本篇提供了实质信息 → changed = true。

changed = true 时,给出 updatedSemantics:在 known_semantics 原有文字的基础上,**追加**一条讲清楚新义(什么语境下取此义、与旧义的区别、是否可直译);不要重写或删改旧义,不要堆砌罗列、不要把同一义项换着说法写两遍。changed = false 时 updatedSemantics 填 null。

【任务类型】{{ task_type }}

【本篇原文】
{{ source_text }}

【待判断的词条】
{{ for i in items }}
- english_expr: {{ i.english_expr }}
  本篇推荐译法: {{ i.this_chinese }}
  本篇语境笔记: {{ i.this_note }}
  已记录语义(known_semantics): {{ i.known_semantics }}
{{ end }}

严格只输出以下 JSON,不要输出 markdown 代码块围栏之外的任何文字。所有字符串用双引号,不得出现尾随逗号,确保整体可被 JSON 解析:
{
  "results": [
    {"englishExpr": "<原样照抄上面的 english_expr>", "changed": true 或 false, "updatedSemantics": "<changed=true 时填并入新义后的完整文本;changed=false 时填 null>"}
  ]
}
上面每一个词条都要在 results 里出现且仅出现一次。
$tpl$,
    1,
    TRUE
);

COMMIT;

-- 验证:
-- SELECT template_type, layer, exam_type_id, subject_category, version, is_active
-- FROM prompt_templates WHERE template_type = 'vocab_semantic_drift';
-- -> 唯一一行:layer='exam_specific', exam_type_id='11111111-1111-1111-1111-111111111111',
--    subject_category=NULL, version=1, is_active=true
