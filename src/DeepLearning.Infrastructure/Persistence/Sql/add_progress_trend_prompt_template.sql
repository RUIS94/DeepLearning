-- =====================================================================
-- Step 9 (进度分析): progress_snapshots.trend_note/key_turning_point — same
-- recurring reason as every prior step's own content-injection row
-- (question_gen/Step 3, grading/Step 4, followup/Step 5, deep_learning/Step 7):
-- no prompt_templates row existed for template_type='progress_trend' before
-- this, because GenerateProgressTrendSnapshotCommandHandler didn't exist when
-- the seed data was written.
--
-- This is shared_methodology/translation (not exam_specific) — narrating a
-- band/pass-rate trend across weeks is generic to any translation-shaped exam
-- type, not NAATI-CT-specific, same scoping as the deep_learning row.
--
-- Deliberately given ONLY the numeric aggregates for the current week and the
-- trailing few weeks (see GenerateProgressTrendSnapshotCommandHandler's own
-- doc comment) — no submission content, no grading_results, no
-- meaning_checkpoints — this is a statistics-narration task, not a grading or
-- follow-up task, so it needs none of the isolation-sensitive material those
-- other AI calls guard against leaking.
--
-- If progress trend generation ever starts failing to parse, check this row
-- (prompt_templates where template_type='progress_trend' AND
-- layer='shared_methodology') hasn't been deactivated or superseded
-- incompatibly.
-- =====================================================================

BEGIN;

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    NULL,
    'translation',
    'progress_trend',
    'shared_methodology',
    $tpl$
以下是一名学习者在"{{ difficulty_tier }}"难度下,按自然周统计的翻译练习表现数据(Band分制,1为最佳、5为最差;pass_rate为通过率百分比)。

【本周】{{ current.period_start }} ~ {{ current.period_end }}
- meaning_transfer平均Band:{{ current.avg_band_meaning_transfer }}
- textual_norms平均Band:{{ current.avg_band_textual_norms }}
- language_proficiency平均Band:{{ current.avg_band_language_proficiency }}
- 通过率:{{ current.pass_rate }}%

【近期历史,按时间由远到近】
{{ if history.size > 0 }}
{{ for w in history }}
- {{ w.period_start }} ~ {{ w.period_end }}:meaning_transfer={{ w.avg_band_meaning_transfer }}, textual_norms={{ w.avg_band_textual_norms }}, language_proficiency={{ w.avg_band_language_proficiency }}, 通过率={{ w.pass_rate }}%
{{ end }}
{{ else }}
(无更早的历史数据,本周是可用的第一条记录)
{{ end }}

请结合本周数据与历史趋势,完成:
1. 用1-2句中文简要点评本周相对历史的变化趋势(进步/退步/持平,具体体现在哪个维度),给出对学习者有实际帮助的一句话建议。语气客观、鼓励,不要空泛套话。
2. 判断本周是否构成一个"关键学习节点"(key turning point)——即出现了历史数据中不曾有过的、值得特别标记的转折,如某维度首次由不及格转为及格、某维度连续多周下滑后本周止跌、或出现历史最佳/最差成绩。如果只是正常的小幅波动,不算关键节点。

【输出格式】
严格只输出以下JSON,不要输出markdown代码块围栏之外的任何文字:
{
  "trendNote": "<1-2句中文趋势点评与建议>",
  "keyTurningPoint": <true 或 false>
}
$tpl$,
    1,
    TRUE
);

COMMIT;
