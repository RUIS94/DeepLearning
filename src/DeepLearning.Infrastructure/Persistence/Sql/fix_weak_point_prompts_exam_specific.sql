-- =====================================================================
-- 把薄弱点相关的三个 prompt_templates(weak_point_classification /
-- weak_point_detection_criteria / weak_point_recheck)从 shared_methodology
-- 改成 NAATI CT 英译中专属(exam_specific),和 grading 现在的做法一致——
-- 这三个调用本来就只服务于翻译评判后的薄弱点流程,没有必要保持"通用"。
--
-- 决定保留三者各自独立的 AiOperationType/ai_call_logs 记录(不像 grading 那样
-- 合并成一个多 stage 的操作),只改考试类型归属这一件事;
-- WeakPointClassifier/WeakPointDetectionCriteriaGenerator/WeakPointRecheckService
-- 三个 C# 服务本来就把 examTypeId 传给了 BuildPromptAsync,不需要改代码。
--
-- 只更新当前 is_active=true 的那几行(旧版本已停用的历史行不用管)。
-- exam_type_id 沿用 seed_naati_ct_en_zh.sql 的固定字面量 UUID。
--
-- 手动执行(Supabase SQL Editor 或 psql)。幂等:UPDATE 对已改过的行无副作用。
-- =====================================================================

BEGIN;

UPDATE prompt_templates
SET exam_type_id = '11111111-1111-1111-1111-111111111111',
    subject_category = NULL,
    layer = 'exam_specific'
WHERE template_type IN ('weak_point_classification', 'weak_point_detection_criteria', 'weak_point_recheck')
  AND is_active = TRUE;

COMMIT;

-- 验证:
-- SELECT template_type, layer, exam_type_id, subject_category, version, is_active
-- FROM prompt_templates
-- WHERE template_type IN ('weak_point_classification', 'weak_point_detection_criteria', 'weak_point_recheck')
--   AND is_active = TRUE;
-- -> 三行都应为 layer='exam_specific', exam_type_id='11111111-1111-1111-1111-111111111111', subject_category=NULL
