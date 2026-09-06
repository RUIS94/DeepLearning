-- =====================================================================
-- 2026-09-06: progress_trend 提示词整表重置为唯一一行 production v1 +
-- exam_specific(NAATI CT 英译中)。
--
-- 变更点:
--   1. 作用域从 shared_methodology / subject_category='translation' 改为
--      exam_specific / exam_type_id='11111111-...'。原先的说法是「按周叙述
--      Band/通过率趋势对任何翻译类考试都通用」,但实际点评质量依赖考试专属
--      事实——三个维度各自的官方通过线(meaning_transfer/language_proficiency
--      为 Band 2、textual_norms 为 Band 3)、三维度须同时达标才整体通过——
--      这些写死进模板才谈得上「首次跨过通过线」这类判断。与 deep_learning /
--      weak_point 三提示词同期的 exam_specific 化同一方向。
--   2. DELETE + INSERT 整表重置:删掉旧的 shared_methodology 行,避免
--      ExamConfigLoader 把 shared 段和 exam_specific 段拼成两份提示词。
--   3. 提示词本身重写:
--      - 显式给出三维度通过线,要求 AI 按「达标 / 临界 / 未达标」而非笼统
--        「进步 / 退步」来点评,并据此收窄 keyTurningPoint 判据(首次跨线 /
--        连续下滑后止跌 / 历史最佳最差 / 通过率首次触达或跌离 100%)。
--      - trendNote 必须点名具体维度 + 变化量 + 一条针对最该改进维度的可执行
--        建议;禁止空泛套话和数字复读。
--      - 空维度值(该周该维度无练习)不得做任何推断。
--      - 输出卫生:与本轮其它提示词一致的严格 JSON 约束措辞。
--
-- 模板变量(GenerateProgressTrendSnapshotCommandHandler 提供,未改动):
--   difficulty_tier                         : "easy" | "medium" | "hard"
--   current.period_start / period_end       : yyyy-MM-dd
--   current.avg_band_meaning_transfer       : 数值或空(该周该维度无练习时为空)
--   current.avg_band_textual_norms          : 同上
--   current.avg_band_language_proficiency   : 同上
--   current.pass_rate                       : 0~100 的整体通过率
--   history[]                               : 同结构,按时间由远到近,可能为空
--
-- 手动执行(Supabase)。幂等:DELETE + INSERT。
-- =====================================================================

BEGIN;

DELETE FROM prompt_templates
WHERE template_type = 'progress_trend';

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    NULL,
    'progress_trend',
    'exam_specific',
    $tpl$
你是 NAATI CT 英译中考试的学习教练。下面是一名学习者在「{{ difficulty_tier }}」难度下、按自然周汇总的翻译练习表现数据。数据仅来自该难度档位,不要与其它档位横向比较。

评分为 Band 制:1 分最好、5 分最差,数值越低越好。本考试 Task A 的三个维度及其官方通过线如下(维度平均 Band 达到或低于通过线即为「达标」):
- meaning_transfer(意义传递):通过线 Band 2
- textual_norms(语篇规范):通过线 Band 3
- language_proficiency(语言能力):通过线 Band 2
三个维度必须同时达标,整份译文才算通过;pass_rate 是按篇计的整体通过率(百分比)。

【本周】{{ current.period_start }} ~ {{ current.period_end }}
- meaning_transfer 平均 Band:{{ current.avg_band_meaning_transfer }}(通过线 Band 2)
- textual_norms 平均 Band:{{ current.avg_band_textual_norms }}(通过线 Band 3)
- language_proficiency 平均 Band:{{ current.avg_band_language_proficiency }}(通过线 Band 2)
- 整体通过率:{{ current.pass_rate }}%
(某维度值为空表示本周该维度没有可统计的练习,不要对空值做任何推断。)

【近期历史,按时间由远到近】
{{ if history.size > 0 }}
{{ for w in history }}
- {{ w.period_start }} ~ {{ w.period_end }}:meaning_transfer={{ w.avg_band_meaning_transfer }}, textual_norms={{ w.avg_band_textual_norms }}, language_proficiency={{ w.avg_band_language_proficiency }}, 通过率={{ w.pass_rate }}%
{{ end }}
{{ else }}
(无更早的历史数据,本周是可用的第一条记录。)
{{ end }}

请完成两件事:

1. trendNote —— 用 1~2 句中文点评本周表现,必须满足:
   - 对每个「有数据」的维度,说明它当前相对自己的通过线是「达标 / 临界 / 未达标」(差 0.3 Band 以内算临界)。
   - 指出本周相对历史的变化方向(进步 / 退步 / 持平),并点名是哪个维度、大致变化了多少 Band 或多少个百分点。
   - 给出一条可立即执行的建议,针对当前最该改进的那个维度(通常是离通过线最远、或本周退步最明显的维度)。
   - 语气客观、鼓励,但不得粉饰未达标的维度;不要空泛套话,不要照抄复读上面已列出的数字。
   - 若历史为空,只点评本周现状与最该改进的维度,不谈趋势。

2. keyTurningPoint —— 判断本周是否是一个值得特别标记的「关键学习节点」。满足下列任一条、且不是偶然的小幅波动时为 true:
   - 某维度的平均 Band 首次由「未达标」跨到「达标」,或首次由「达标」跌回「未达标」。
   - 某维度在历史中连续至少 2 个周期持续变差之后,本周首次止跌回升。
   - 某维度平均 Band 或整体通过率创下这段历史中的最好或最差纪录。
   - 整体通过率首次达到 100%,或首次从 100% 跌下来。
   若通过线两侧归属没有变化,且各维度变化幅度都在 ±0.3 Band 以内、通过率变化在 ±10 个百分点以内,视为正常波动,keyTurningPoint 为 false。

【输出格式】
严格只输出下面这个 JSON,不要输出 markdown 代码块围栏,也不要输出 JSON 以外的任何文字。所有字符串用双引号,不得出现尾随逗号,确保整体可被 JSON 解析:
{
  "trendNote": "<1~2 句中文趋势点评与建议>",
  "keyTurningPoint": true 或 false
}
$tpl$,
    1,
    TRUE
);

COMMIT;

-- 验证:
-- SELECT template_type, layer, exam_type_id, subject_category, version, is_active
-- FROM prompt_templates WHERE template_type = 'progress_trend';
-- -> 唯一一行:layer='exam_specific', exam_type_id='11111111-1111-1111-1111-111111111111',
--    subject_category=NULL, version=1, is_active=true
