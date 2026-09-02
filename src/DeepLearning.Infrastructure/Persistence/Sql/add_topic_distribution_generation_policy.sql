-- =====================================================================
-- M11 — generation_policy:题材随机比例
--
-- GenerateQuestionCommandHandler.ResolveTopicHintAsync 在调用方未指定 CategoryId 时,
-- 按此比例掷骰决定是否从已有 domain 分类里随机挑一个作为「题材倾向」软提示
-- (命中则注入 question_gen prompt 的 {{ topic_hint }};未命中则让 AI 自由选题)。
-- 取值套路同 difficulty_distribution / weak_point_targeting_ratio。
--
-- 手动执行(Supabase)。幂等:UNIQUE(exam_type_id, policy_key) + ON CONFLICT。
-- =====================================================================

BEGIN;

INSERT INTO generation_policy (exam_type_id, policy_key, policy_value)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    'topic_distribution',
    '{"topic_random_ratio": 0.5}'::jsonb
)
ON CONFLICT (exam_type_id, policy_key) DO NOTHING;

COMMIT;

-- 验证:
-- SELECT policy_key, policy_value FROM generation_policy
-- WHERE exam_type_id = '11111111-1111-1111-1111-111111111111' ORDER BY policy_key;
