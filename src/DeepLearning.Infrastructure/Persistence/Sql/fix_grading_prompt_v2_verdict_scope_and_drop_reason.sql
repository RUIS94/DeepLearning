-- =====================================================================
-- Four surgical corrections to the ACTIVE grading v2 template, applied in place.
--
-- WHY IN PLACE, AND WHY A SEPARATE FILE:
--   rebuild_grading_prompt_three_stage.sql already carries the corrected text, so a
--   FRESH database installs the right thing from one readable file — that file stays
--   the single source of truth for the grading rubric, which is the whole point after
--   the v1 incident where the live prompt existed only in the DB.
--   But SqlScriptRunner is strictly forward-only: a database that already recorded
--   rebuild_grading_prompt_three_stage.sql will never re-run it, so an edit to that
--   file alone leaves an applied DB on the pre-fix text. This script closes that gap.
--
--   version is deliberately NOT bumped. A fresh DB gets version 2 with the corrected
--   text; an already-applied DB should land on exactly the same state, not on a
--   version number that only exists in one of the two environments. No submission was
--   graded with the pre-fix text (same working session), so there is no result history
--   whose provenance this erases — the usual reason AGENTS.md #9 forbids in-place edits
--   does not apply here.
--
-- WHAT CHANGED (all four were review findings on the v2 draft, not bugs):
--
--  1. VERDICT STAGE — source_text / submission_content had no assigned purpose.
--     The stage's whole discipline is "evidence is final, do not find new errors", yet
--     the full source and translation sat in its context window with no instruction
--     covering them. That is the same failure mode as #6 in the rebuild file's header
--     (checklist out-competing the Band text), one size down: unassigned raw material
--     invites the model to form its own impression and quietly re-grade off-evidence.
--     They are NOT removed, because the official descriptions grade by PROPORTION
--     ("mostly" / "some" / "isolated" / "frequent") — three minor errors in a 200-word
--     text and in a 2,000-word text do not fit the same band, and that denominator only
--     exists in the full text. So they get an explicit, bounded licence instead
--     (new clause 一.7): confirm the excerpts are not out of context, judge proportion,
--     and nothing else — no new entries, no overturning, no silent Band adjustment.
--
--  2. AUDIT STAGE — the drop-reason schema hint was narrower than the rule it points at.
--     復核纪律 2① covers style preference, synonymous rewording, acceptable term
--     variants AND legitimate translation technique (word-class shift, splitting or
--     merging sentences, reordering, making an implicit subject explicit). The JSON
--     example compressed all of that to 「非偏差,属风格偏好」. A model treating a schema
--     placeholder as an enumeration would then hesitate to drop a finding that is really
--     just a legitimate technique — i.e. keep a non-error in the evidence base, which is
--     exactly what the "译文答案不唯一" principle exists to prevent. Both the rule and
--     the hint now spell out the same set, and the hint explicitly forbids 「太轻微」.
--
--  3. EVIDENCE STAGE — checkpoint numbering came from Scriban's implicit {{ for.index }}.
--     It rendered correctly, but it was the template's only use of that variable and it
--     was a SECOND, independent numbering of the same list: the prompt numbered via
--     Scriban while GradeSubmissionCommandHandler.BuildCheckpointVerdicts pairs the
--     model's answers back on via C# `i + 1`. They agreed by coincidence. The index is
--     now a real field on the template model ({{ cp.index }}), so both come from one
--     place, and PromptRendererThreeStageGradingTemplateTests renders three checkpoints
--     to pin that it really emits 1, 2, 3.
--
--  4. HEADER (comment only, no template text) — the claim that a seed needs "no code
--     change" was true but untested and, in this database, unused. The path is real:
--     LlmClientResolver.ConfiguredLlmClient merges llm_provider_settings.extra_settings
--     onto the request, OpenAiCompatibleLlmClient writes every key into the body, and the
--     grading call passes ExtraSettings: null so it does not shadow it (now covered by
--     OpenAiCompatibleLlmClientTests "Forwards a seed ..."). But the active provider
--     (mimo) has extra_settings NULL, so no seed is being sent. Setting one:
--       PUT /api/v1/llm-providers/mimo  {"extraSettingsJson": "{\"seed\": 7}"}
--
-- Each replacement is asserted: if the live text has drifted (someone hot-edited the
-- template via PUT /api/v1/prompt-templates/{id}), the matching UPDATE affects 0 rows,
-- the assertion raises, and the whole transaction rolls back rather than half-applying.
-- =====================================================================

BEGIN;

DO $mig$
DECLARE
    tpl_id uuid;
    body   text;
    -- Match the line ending the stored document already uses. `sql apply` sends whatever
    -- the checked-out .sql file has, and the root .gitattributes normalises these to CRLF
    -- on Windows, so the live template is CRLF while a LF checkout would make it LF.
    -- Inserting a bare newline either way would leave the patched text a few characters
    -- adrift from what a fresh install of rebuild_grading_prompt_three_stage.sql produces,
    -- which is exactly the convergence this file promises.
    nl     text;
BEGIN
    SELECT id, template_content INTO tpl_id, body
    FROM prompt_templates
    WHERE template_type = 'grading' AND is_active = TRUE;

    IF tpl_id IS NULL THEN
        RAISE EXCEPTION 'no active grading template row found';
    END IF;

    nl := CASE WHEN position(E'\r\n' in body) > 0 THEN E'\r\n' ELSE E'\n' END;

    -- Already corrected (re-run, or a fresh DB that installed the fixed rebuild file).
    IF position('只有两个被许可的用途' in body) > 0 THEN
        RAISE NOTICE 'grading template already carries the v2 corrections — nothing to do';
        RETURN;
    END IF;

    -- (1) verdict stage: bounded licence for the source text and the translation.
    body := replace(
        body,
        '6.【不要考虑是否过线】。通过与否由后端按官方通过线机械判定,通过概率也由后端计算。你的输出里没有这两项,也不得让它们影响选档。',
        '6.【不要考虑是否过线】。通过与否由后端按官方通过线机械判定,通过概率也由后端计算。你的输出里没有这两项,也不得让它们影响选档。' || nl ||
        '7. 第四节给出的原文与译文全文,只有两个被许可的用途:' || nl ||
        '   ① 确认下方摘录的证据没有断章取义;' || nl ||
        '   ② 官方描述里 mostly / some / isolated / frequent / consistently 这类措辞是【比例】判断——同样 3 条 minor,在 200 词短文里和在 2000 词长文里贴合的档位不同,所以必须知道全文有多长、受影响的比例有多大。' || nl ||
        '   除此之外一律不得使用:不得据此新增证据条目,不得推翻或弱化既有条目,更不得因为"我自己又看出一处问题"而调整 Band。凡是不在证据清单里的问题,对本阶段而言一律当作不存在。');

    body := replace(body, '四、已定稿的评判材料', '四、已定稿的评判材料(原文与译文的用途受一.7 限制)');

    -- (2) audit stage: the drop rule and its schema hint must span the same set of cases.
    body := replace(
        body,
        '2. drop 只有两个正当理由:① 它其实不是偏差——违反"译文答案不唯一"原则,属于风格偏好、同义改写,或原文成分虽无逐字对应但信息/逻辑/范围/语气/指代都在;② 与另一条 finding 重复。',
        '2. drop 只有两个正当理由:' || nl ||
        '   ① 它其实不是偏差——违反"译文答案不唯一"原则。这一类包含(不限于):风格偏好与同义改写;公认可接受的多个术语译名之一;以及【合理翻译手段】——词性转换、拆句合句、语序调整、显化隐含逻辑主语等,只要信息、逻辑关系、范围、语气强度、指代全部保留,就不是偏差。' || nl ||
        '   ② 与另一条 finding 指的是同一处。');

    body := replace(
        body,
        '{"id": "F3", "action": "drop", "reason": "<只能是「非偏差,属风格偏好」或「与 Fx 重复」>"}',
        '{"id": "F3", "action": "drop", "reason": "<按复核纪律 2 二选一:「非偏差」——含风格偏好、同义改写、可接受的术语异名、以及词性转换/拆合句/语序调整/显化隐含逻辑主语等合理翻译手段;或「与 Fx 重复」。不得填「太轻微」「不至于扣分」>"}');

    -- (3) evidence stage: checkpoint numbering now comes from the template model.
    body := replace(body, '- [{{ for.index + 1 }}] ({{ cp.importance }})', '- [{{ cp.index }}] ({{ cp.importance }})');

    UPDATE prompt_templates SET template_content = body WHERE id = tpl_id;

    -- Assert every correction actually landed, or roll the whole thing back.
    IF position('只有两个被许可的用途' in body) = 0
        OR position('一.7 限制' in body) = 0
        OR position('合理翻译手段' in body) = 0
        OR position('不得填「太轻微」' in body) = 0
        OR position('{{ cp.index }}' in body) = 0
        OR position('{{ for.index + 1 }}' in body) > 0
    THEN
        RAISE EXCEPTION 'grading template text has drifted from the expected v2 wording — no correction applied';
    END IF;
END
$mig$;

COMMIT;

-- Verify:
--   SELECT version, is_active, length(template_content),
--          position('只有两个被许可的用途' in template_content) > 0 AS verdict_scope_fixed,
--          position('{{ cp.index }}' in template_content) > 0     AS checkpoint_index_fixed
--   FROM prompt_templates WHERE template_type = 'grading' ORDER BY version;
