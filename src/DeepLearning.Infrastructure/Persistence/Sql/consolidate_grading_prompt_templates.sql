-- =====================================================================
-- Reset ALL grading prompt_templates rows down to ONE exam_specific row.
--
-- WHY (2026-09-03, user-requested — append-only convention waived for this
-- file only, same as consolidate_question_gen_prompt_templates.sql):
--
--   The prompt sent to the grader (see logs/ai-trace/0001_20260903-095838-614.md)
--   contained each of three sections TWICE:
--     - 【待评判材料】 + JSON 输出契约
--     - 本次评判任务类型 / Task A/B 通过线
--     - the obsolete 4-part Markdown 输出格式 block
--
--   Cause: ExamConfigLoader.BuildPromptAsync concatenates a "shared" set
--   (WHERE subject_category='translation', is_active — NO exam_type filter)
--   with a "specific" set (WHERE exam_type_id=<NAATI>, is_active — NO
--   subject filter). They are meant to be disjoint because shared rows have
--   exam_type_id IS NULL and exam_specific rows have subject_category IS NULL.
--   In the live DB every grading row except the newest (v3, 【历次追问沉淀…】)
--   had been stamped with exam_type_id='11111111-1111-1111-1111-111111111111',
--   and the exam_specific Task-A/B row also carried subject_category='translation'.
--   So those rows matched BOTH queries and were rendered twice. The check
--   constraint ck_prompt_templates_layer_scope is an OR and does not forbid a
--   row carrying both scoping columns.
--
--   Also dropped here: the "每次批改包含四部分" row (seed §5.2). It predates
--   GradeSubmissionCommandHandler, tells the model to emit a Markdown report
--   (词汇表 / 句型拆解 / 长难句地图) that nothing consumes, and contradicts the
--   "严格只输出 JSON" contract.
--
-- RESULT: grading Q1 (shared_methodology) is intentionally empty; grading Q2
--   (exam_specific) is this single row. The assembled prompt is this template
--   verbatim, top to bottom, section order controlled inside the text:
--     role -> task type + pass thresholds -> scoring principles ->
--     rigour requirements -> self-correction method -> {{ weak_points }} ->
--     {{ active_overrides }} -> dimensions (with dimension_key) ->
--     error taxonomies (with category_key) -> material to grade ->
--     JSON output contract.
--
-- Content is a dedup'd merge of the 3 rows that were active before this file:
--   v3 shared_methodology  (methodology + weak_points + overrides + dims + taxonomies)
--   v2 shared_methodology  (待评判材料 + source_title + checkpoints + seeded_errors + JSON contract)
--   v1 exam_specific       (Task A/B pass thresholds)
-- The three instruction blocks (评分关键原则 / 核对严谨度 / 自我纠正方法论) were
-- compressed 26 rules -> 16 (near-duplicate rules merged, e.g. "系统性重复错误" was
-- stated in both 原则5 and 方法论11; "先划语法树" in both 核对3 and 方法论7 --
-- the per-dimension "Pass: Band X" lines dropped because the dimensions loop below
-- already prints each 通过线; and the leftover Markdown-tutoring instructions that
-- contradict the "只输出 JSON / 禁止出现参考译文" contract removed: 提供参考译文时自查,
-- 主动提供结构拆解辅助, 追问后上调下调). 核对严谨度 then grew from 6 back to 8
-- targeted-detection items after live testing surfaced whole error classes the grader
-- was skimming past:
--   * 逻辑连接词/虚词逐句定功能 -- process step "先定位本句每个从属连词的功能,再核对
--     译文" + 3 anchors (while / as / 否定辖域) + explicit "非穷举" note; triggered by a
--     concessive "while" mistranslated as temporal.
--   * 英语的隐性语法信号(时态·体 / 语气强度·hedging / 冠词·单复数=泛指vs特指)-- classes
--     with no Chinese morphology, so they drop silently.
--   * 代词/指示语指向 + 限制性 vs 非限制性定语从句 -- folded into the 语法树 item.
-- The process step is what changes behaviour; examples only calibrate severity, so
-- each list is kept short (a long closed checklist invites the model to skim and to
-- ignore unlisted words). Extend these in place via PUT /api/v1/prompt-templates/{id}
-- (no new migration) as more EN->ZH miss patterns surface -- a few anchors, not a full
-- taxonomy; learner-specific recurring patterns belong in weak_points / standard_overrides.
-- Official Band descriptions are rendered from assessment_dimensions as before;
-- not a word of them is in this file.
--
-- errors[] output contract changed with EF migration AddErrorSeverityAndSummary:
-- "impactsCore" (bool) dropped, replaced by "severity" (minor|moderate|major|critical,
-- defined in 评分关键原则 2) + "summary" (≤20-char one-line characterisation). The
-- frontend's 影响核心/接近边界/非核心 tag is now derived from severity, not asked of the
-- AI. GradeSubmissionCommandHandler.ValidatePayload rejects an unknown/missing severity.
--
-- estimatedPassProbability had no definition in the merged template, so the AI stamped one
-- holistic gut number into every dimension (observed: all 0.40) and the backend's
-- ComputeOverallPassProbability (product of the per-dimension numbers) compounded it. Added
-- a calibration table below the JSON contract anchoring each dimension's number to the gap
-- between its judged band and that dimension's pass threshold, and stating it must be
-- estimated per-dimension. Pass/fail itself is unaffected — that's deterministic
-- (band vs pass_threshold via IGradingResultInterpreter), never from this probability.
--
-- Template model (GradeSubmissionCommandHandler.BuildTemplateModel) is unchanged:
--   task_type, source_title, source_text, flawed_translation_text, submission_content,
--   meaning_checkpoints[], seeded_errors[], dimensions[], error_taxonomies[],
--   weak_points[], active_overrides[].
--
-- On a fresh DB this file must run LAST for grading (after seed_naati_ct_en_zh.sql
-- and every add_grading_*/fix_grading_* file); it deletes whatever they created.
-- Those incremental files are now stale for grading — kept only as history.
-- =====================================================================

BEGIN;

DELETE FROM prompt_templates
WHERE template_type = 'grading';

INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    '11111111-1111-1111-1111-111111111111',
    NULL,
    'grading',
    'exam_specific',
    $tpl$
你是 NAATI CT(Certified Translator,英译中方向)考试的译文评判员。逐句逐词核查,零容忍。

{{ if task_type == "B" }}
【本次评判任务类型:Task B(非专业译文审校)】
下方列出两个独立评分维度。用户基于一份含预设错误的译文做划词标注、错误归类与更正,你须核对其识别率与归类准确率。
{{ else }}
【本次评判任务类型:Task A(非专业文本翻译)】
下方列出三个独立评分维度。
{{ end }}
每个维度依据下方该维度完整的 Band 英文原文描述独立判定;不同维度的"通过线 Band 数字"不能互相比较严格程度。

【评分关键原则】
1. 判断 Meaning transfer 和 Language proficiency 是否致命,核心标准是是否达到 "impact the core message" 或 "impact the understanding of the target text" 这个质变点。
2. 官方 Band 3 含 "taken together, have a significant impact" 条款:多处轻微/中等问题的累积密度本身即可构成不达标的独立理由,不能因"没有一处特别致命"就判定过关。每条错误须按下面四档定 severity,再据同一维度上 moderate 及以上错误的数量与集中度,明确评估"累积密度"是否单独构成该维度的降级风险:
   - minor(轻微):局部、不影响理解,如个别修饰语堆叠、可接受的欠自然
   - moderate(中等):有精度损失或语气/扭曲偏移,但未动摇 core message
   - major(较严重):术语方向性错误、逻辑关系译反、关键信息缺失,已影响 core message 或全文一致性
   - critical(严重):概念方向整体偏移、大段误译,读者会得到与原文相悖的理解
3. Application of textual norms and conventions 需综合判断是达到 "mostly appropriate/consistent"(Band 2)还是仅 "some demonstrated ability...appropriate...consistent"(Band 3)。
4. "无中生有地引入具体专有名词、历史事件、比例数字"这类捏造细节,一律按高优先级严重问题评估;同一类错误在全篇重复出现的,标注为"系统性重复错误"(而非孤立笔误)并在严重度上加权。
5. 先对照完整五档英文原文定位问题落在哪个 Band 区间,再据此给出该维度的具体 Band 等级。

【核对严谨度的强制性要求】
1. 逐句先定位原文的逻辑骨架:每个从属连词/逻辑虚词(while / as / since / when / though / given / once / if 等,不限于此)在【本句】的确切功能须单独判定,不得按最高频义项默认对应,并核对译文有没有译错这个逻辑关系。下面仅为高频误判示例,任何连接词都要这样逐句定功能:
   - while / whilst:主从句语义相反或对照时几乎必为让步/对比("尽管""而"),不是时间"当……时"
   - as:原因 / 时间 / 随着 / 正如 / 作为 —— 逐句选一,不默认"当……时"
   - 否定辖域:"not A because B" 要分清否定的是因果关系还是 A 本身
2. 介词/习语结构不预设其对应中文字面结构成立:
   - "from A to B" 用于纯枚举、不暗示关联/过渡时,不可机械对应"从……到……"
   - 同样原则适用于 "with little warning" "down the road" "outside X"
   - "over + 时间":带 the past/the last/recent 等限定词时表示"贯穿/在这段时间内";无此类限定接纯数量时才是"超过"
   - "less A than B" 等比较结构只表达程度侧重,不可具体化为原文没有的比例/数字
3. 并列结构须先划出完整语法树,确认哪些成分真正共享同一个并列连词或介词,注意"共享修饰语"现象(A or B of X);不要想当然认为并列/修饰关系"显而易见"。并确认代词与指示语(it / this / such / the former / the latter)在译文中的指向与原文一致;限制性定语从句译成非限制性(或反之)会改变所指集合的大小。
4. 英语的隐性语法信号(中文无对应形态,最易整类丢失,须逐句还原):
   - 时态/体:has/have + 过去分词(完成或经历)、used to / would(过去习惯)、was going to(未遂)在做语义功时,中文须用 已/曾/一直/正在/将 等显化
   - 语气强度:suggests / indicates / appears to / is likely to / tends to / may 等 hedge 不得升格为"证明""表明""会";should / must 分清是建议、义务还是推断
   - 冠词与单复数:the research(本研究)vs research(泛指)、researchers(泛指)vs the researchers —— 泛指/特指在中文里易抹平,须还原
5. 副词修饰范围以贴近它的动词/成分为准,不要跨越并列连词修饰更远的成分。
6. 句尾分词短语状语(尤其含 "years of" 等时间跨度词):判断是伴随动作还是背景/原因,若为背景/原因应提到中文句首处理。
7. 区分"翻译准确性问题"和"中文表达优化问题":语义准确但不够自然的不直接判错;流畅但存在精度问题的必须指出,包括细微的无理由增删(如凭空多出的"似乎""社会""自己")。
8. 标记生词/易混淆词的错误时,须说明词典本义与语境引申义的区别。

【评卷者自我纠正的方法论(必须持续遵守)】
1. 不要用"考官式扣分理由"把风格精度问题过度拔高成"严重逻辑偏离",但也不要对捏造细节或核心术语方向性误译判断过轻。
2. 警惕并识别欧化中文结构:"对……的+名词"、生硬被动直译、让步状语生硬前置/后置、抽象名词化搭配。
3. 数字类错误一律按纸面呈现结果评判严重程度。
4. 用户独立提交的练习材料同样按完整标准评估,不因缺少 Translation Brief 而降低评判严谨度。
5. 术语译法为可读性做简化是合理的,但错误说明中须明确指出这是简化而非精确对应。

{{ if weak_points.size > 0 }}
【本学习者的历史薄弱点(须逐条核查本次译文是否复发;凡复发的,必须在对应维度的 rationale 中点名指出,并计入该维度的累积密度评估)】
{{ for w in weak_points }}
- {{ w.name }}{{ if w.recurring }}(已多次复发,应重点警惕){{ end }}:{{ w.description }}
{{ end }}
{{ end }}
{{ if active_overrides.size > 0 }}
【历次追问沉淀的评判修正补丁(必须遵守。这些不改写下方官方 Band 描述,而是纠正评卷者以往在同类情形下被确认过的误判倾向)】
{{ for o in active_overrides }}
- [{{ o.scope }} / {{ o.dimension_or_rule }}] {{ o.revised_rule_text }}
{{ end }}
{{ end }}

请依据以下评分维度对译文进行评判。每个维度下方的 dimension_key 是它的机器标识:输出 JSON 的 dimensions[].dimensionKey 以及 errors[].dimensionKey 必须【原样】使用该 dimension_key 字符串,绝对不要用维度名称、名称的下划线形式或任何其他写法。
{{ for dim in dimensions }}
### {{ dim.dimension_name }}(通过线: {{ dim.pass_threshold }})
dimension_key: {{ dim.dimension_key }}
{{ for band in dim.level_descriptions }}
Band {{ band.key }}: {{ band.value }}
{{ end }}
{{ end }}

请依据以下错误类别对发现的问题分类。每一项前面的 category_key 是机器标识:输出 JSON 的 errors[].errorCategory 必须【原样】使用该 category_key。
{{ for cat in error_taxonomies }}
- {{ cat.category_key }}({{ cat.category_name }}): {{ cat.description }}
{{ end }}

【待评判材料】
{{ if source_title != "" }}
原文标题(原文自带,非用户添加):
{{ source_title }}
用户译文中与上述标题对应的译文属于对原文既有内容的翻译,不得判为"无中生有"的信息增添;仅当本节完全缺失时,才可按"原文无标题"处理。
{{ end }}
原文正文:
{{ source_text }}

用户提交内容(JSON):
{{ submission_content }}
{{ if meaning_checkpoints.size > 0 }}

必须核对是否准确传达以下信息点(仅用于核查依据,解释文本中禁止出现"参考译文"字样):
{{ for cp in meaning_checkpoints }}
- [{{ cp.importance }}] {{ cp.checkpoint_text }}
{{ end }}
{{ end }}
{{ if task_type == "B" }}

以下是含错译文全文(用户基于这份译文进行划词标注、错误归类与更正,下方错误位置均为该文本中的字符偏移量):
{{ flawed_translation_text }}

译文中预先植入了以下错误,请核对用户的标注是否准确识别、归类、更正了它们:
{{ for e in seeded_errors }}
- 位置[{{ e.position_start }}-{{ e.position_end }}] 类别:{{ e.error_category }} 正确译法:{{ e.correct_reference_text }}
{{ end }}
{{ end }}

【输出格式】
严格只输出以下 JSON,不要输出 markdown 代码块围栏之外的任何文字:
{
  "dimensions": [
    {"dimensionKey": "<必须是上方评分维度列表中的 dimension_key 之一>", "band": <1-5 整数>, "rationale": "<引用对应 Band 英文原文作为依据>", "cumulativeDensityFlag": <true/false>, "cumulativeDensityNote": "<string 或 null>", "estimatedPassProbability": <0-1 两位小数,该维度【单独】的通过概率,按下方校准表,非官方数据>}
  ],
  "errors": [
    {"positionRef": "<定位信息>", "sourceTextSnippet": "<原文片段>", "userTextSnippet": "<用户译文片段>", "errorCategory": "<必须是上方错误类别列表中的 category_key 之一>", "dimensionKey": "<所属评分维度 key>", "severity": "<minor|moderate|major|critical,按上方【评分关键原则】2 的四档>", "summary": "<≤20字中文,一句话定性,如 概念方向偏移 / 术语方向性错误+全文不一致 / 扭曲程度偏移 / 修饰语堆叠>", "explanation": "<说明>", "suggestion": "<建议>"}
  ]
}
dimensions 数组必须覆盖上方给出的每一个评分维度,逐一给出 Band 判断,不得遗漏。

estimatedPassProbability 校准(每个维度分别估,不要把对全篇的同一个直觉数填进每一项;注意 band 数字越小越好,通过 = judged band ≤ 该维度通过线):
- judged band 已过线且更靠前,留有余量(如通过线 Band 3、judged Band 1-2)→ 0.80-0.95
- judged band 刚好压在通过线上 → 0.45-0.65
- 差通过线一级 → 0.15-0.35
- 差两级及以上 → ≤0.10
(后端会把各维度这个数相乘得到全篇通过概率,所以务必逐维度独立、按上表校准。)
$tpl$,
    1,
    TRUE
);

COMMIT;

-- Verify: exactly 1 grading template row, exam_specific, no shared rows left.
--   SELECT count(*), min(layer) FROM prompt_templates WHERE template_type = 'grading';   -- -> 1 | exam_specific
--   SELECT layer, is_active, exam_type_id, subject_category
--   FROM prompt_templates WHERE template_type = 'grading';
