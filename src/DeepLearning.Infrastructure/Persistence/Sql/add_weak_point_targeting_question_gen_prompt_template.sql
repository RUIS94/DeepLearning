-- =====================================================================
-- Design doc §10.5 "出题与薄弱点联动": GenerateQuestionCommand gained an optional
-- TargetWeakPoints flag (default false, caller opts in per call — see
-- WeakPointTargetingSelector's own doc comment for why this is deliberately not a global
-- switch). When the caller opts in AND a generation_policy-weighted dice roll actually says
-- to target this call AND the user has an active weak_points row, GenerateQuestionCommandHandler
-- passes that weak point's Category text into the template model as weak_point_hint.
--
-- This adds a new shared_methodology/question_gen row (additive — IExamConfigLoader
-- concatenates every active row per template_type, same convention as the seed_samples and
-- TaskB additions from earlier steps) instructing the AI to lean the generated question's
-- content/error focus toward that category where it fits naturally — a soft nudge, not a hard
-- requirement, matching design doc §10.5's own "不追求100%围绕薄弱点出题" (don't chase 100%
-- weak-point coverage — that would make generated questions too narrow and stop testing
-- whether other areas have regressed). Renders to nothing when weak_point_hint is null
-- (Scriban's {{ if }} is falsy for a C# null), so this is a complete no-op for every call that
-- doesn't opt in or doesn't get selected by the ratio roll.
-- =====================================================================

BEGIN;

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    NULL,
    'translation',
    'question_gen',
    'shared_methodology',
    $tpl$
{{ if weak_point_hint }}
【薄弱点关联提示】
该学员近期在"{{ weak_point_hint }}"这一类别上被反复标记为薄弱点。如果与本次出题的领域/难度要求自然契合,可以在生成的原文或(TaskB场景下)植入的错误中适度体现相关的语言特征或错误类型,帮助针对性练习;但不要为了强行覆盖这一薄弱点而扭曲原文的自然度和真实感,也不必每次都刻意围绕它出题。
{{ end }}
$tpl$,
    1,
    TRUE
);

COMMIT;
