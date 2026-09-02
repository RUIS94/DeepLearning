-- =====================================================================
-- NAATI CT 英译中 —— 评分标准种子数据
-- 来源:NAATI_CT_陪练评卷提示词.md
--
-- 使用前必读:
-- 1. 脚本开头有一处ALTER TABLE,补上schema.sql里漏掉的字段
--    (assessment_dimensions缺少task_type归属,写这批真实数据时才发现)
-- 2. exam_types用固定字面量UUID('11111111-1111-1111-1111-111111111111'),
--    方便本脚本内后续INSERT直接引用,不用写嵌套CTE
-- 3. weak_points那段需要你把'YOUR_USERNAME'换成你在users表里的真实username
-- 4. prompt_templates.template_content里的{{ }}是Scriban占位符,不是真的
--    要在这里渲染,是运行时Exam Config Loader读出来拼装用的
-- =====================================================================

BEGIN;

-- ---------------------------------------------------------------------
-- 补schema:assessment_dimensions需要知道自己属于TaskA还是TaskB,
-- 不然评分引擎运行时不知道该对哪个任务类型取哪几个维度
-- ---------------------------------------------------------------------
ALTER TABLE assessment_dimensions
    ADD COLUMN IF NOT EXISTS applicable_task_type task_type_enum;

-- =====================================================================
-- 一、exam_types(对应文件标题+第一节"英译中")
-- =====================================================================

INSERT INTO exam_types (
    id, code, name, subject_category, source_language, target_language,
    grade_level, description, is_active
) VALUES (
    '11111111-1111-1111-1111-111111111111',
    'naati_ct_en_zh',
    'NAATI CT 英译中',
    'translation',
    'en',
    'zh',
    NULL,
    'NAATI Certified Translator考试,英译中方向。出题偏向真实存在的文章,多为澳大利亚相关场景,专业词汇准确但不刁钻。',
    TRUE
);

-- =====================================================================
-- 二、assessment_dimensions(对应第三、四节:官方评分体系+Band完整描述)
-- rubric_version取自文件第三节"2024年2月最终版"
-- =====================================================================

-- --- Task A: Meaning transfer -----------------------------------------
INSERT INTO assessment_dimensions (
    exam_type_id, dimension_key, dimension_name, scale_type, pass_threshold,
    applicable_task_type, level_descriptions, rubric_version, effective_from,
    source_reference, verified_at
) VALUES (
    '11111111-1111-1111-1111-111111111111',
    'meaning_transfer',
    'Meaning transfer',
    'band_1_5',
    'Band 2 or above',
    'A',
    '{
        "1": "Translates the intent and consistently translates the content of the message accurately. Minimal or no distortions, unjustified omissions and/or unjustified additions.",
        "2": "Translates the intent and mostly translates the content of the message accurately. The distortions, unjustified omissions and/or unjustified additions have a minor impact on the overall precision of the meaning transfer but do not impact the core message.",
        "3": "Some demonstrated ability to translate the intent and content of the message accurately. The distortions, unjustified omissions and/or unjustified additions, taken together, have a significant impact on the overall precision of the meaning transfer. and/or One or more distortions and/or unjustified omissions and/or unjustified additions impact the core message.",
        "4": "Limited demonstrated ability to translate the content and intent of the message accurately. Frequent distortions, unjustified omissions and/or unjustified additions.",
        "5": "Minimal or no demonstrated ability to translate the content and intent of the message accurately. Excessive distortions, unjustified omissions and/or unjustified additions."
    }'::jsonb,
    '2024-02',
    now(),
    'NAATI Certified Translator Assessment Rubrics(2024年2月最终版)',
    now()
);

-- --- Task A: Application of textual norms and conventions --------------
INSERT INTO assessment_dimensions (
    exam_type_id, dimension_key, dimension_name, scale_type, pass_threshold,
    applicable_task_type, level_descriptions, rubric_version, effective_from,
    source_reference, verified_at
) VALUES (
    '11111111-1111-1111-1111-111111111111',
    'textual_norms',
    'Application of textual norms and conventions',
    'band_1_5',
    'Band 3 or above',
    'A',
    '{
        "1": "Demonstrates accomplished use of register, style, text structure and domain-specific terminology in a way that is appropriate for the genre and target audience and consistent with the norms and conventions of the target language.",
        "2": "Demonstrates ability in the use of register, style, text structure and domain-specific terminology in a way that is mostly appropriate for the genre and target audience and mostly consistent with the norms and conventions of the target language.",
        "3": "Some demonstrated ability to use register, style, text structure and domain-specific terminology in a way that is appropriate for the genre and target audience and consistent with the norms and conventions of the target language.",
        "4": "Limited demonstrated ability to use register, style, text structure and domain-specific terminology in a way that is appropriate to the genre and target audience and consistent with the norms and conventions of the target language.",
        "5": "Minimal or no demonstrated ability in the use of register, style, text structure and domain-specific terminology appropriate to the genre and target audience and consistent with the norms and conventions of the target language."
    }'::jsonb,
    '2024-02',
    now(),
    'NAATI Certified Translator Assessment Rubrics(2024年2月最终版)',
    now()
);

-- --- Task A: Language proficiency ---------------------------------------
INSERT INTO assessment_dimensions (
    exam_type_id, dimension_key, dimension_name, scale_type, pass_threshold,
    applicable_task_type, level_descriptions, rubric_version, effective_from,
    source_reference, verified_at
) VALUES (
    '11111111-1111-1111-1111-111111111111',
    'language_proficiency',
    'Language proficiency (Target language)',
    'band_1_5',
    'Band 2 or above',
    'A',
    '{
        "1": "Consistently uses written language competently and idiomatically. Any unidiomatic usage and/or errors of lexicon, grammar, syntax, spelling and/or punctuation are isolated and do not impact the overall quality of the translation.",
        "2": "Mostly uses written language competently and idiomatically. The unidiomatic usage and/or errors of lexicon, grammar, syntax, spelling and/or punctuation have a minor impact on the overall quality of the translation but do not impact the understanding of the target text.",
        "3": "Some demonstrated ability to use written language competently and idiomatically. The unidiomatic usage and/or errors of lexicon, grammar, syntax, spelling and/or punctuation have a significant impact on the overall quality of the translation. and/or One or more errors impact the understanding of the target text.",
        "4": "Limited demonstrated ability to use written language competently and idiomatically. Unidiomatic usage and/or errors of lexicon, grammar, syntax, spelling and/or punctuation frequently impact the understanding of the target text.",
        "5": "Minimal or no demonstrated ability to use written language competently and idiomatically. Unidiomatic usage and/or errors in the use of lexicon, grammar, syntax, spelling and/or punctuation constantly impact the understanding of the target text."
    }'::jsonb,
    '2024-02',
    now(),
    'NAATI Certified Translator Assessment Rubrics(2024年2月最终版)',
    now()
);

-- --- Task B: Revision skills ---------------------------------------------
INSERT INTO assessment_dimensions (
    exam_type_id, dimension_key, dimension_name, scale_type, pass_threshold,
    applicable_task_type, level_descriptions, rubric_version, effective_from,
    source_reference, verified_at
) VALUES (
    '11111111-1111-1111-1111-111111111111',
    'revision_skills',
    'Revision skills',
    'band_1_5',
    'Band 2 or above',
    'B',
    '{
        "1": "There are no or almost no remaining and/or introduced errors in the revised translation.",
        "2": "There are a few remaining and/or introduced errors in the revised translation, which have a minor impact on the overall quality of the translation but do not impact the core message and understanding.",
        "3": "There are several remaining and/or introduced errors in the revised translation that have a significant impact on the overall quality of the translation and/or there is one or more errors that impact the core message and/or understanding.",
        "4": "There are frequent remaining and/or introduced errors in the revised translation.",
        "5": "There is no or almost no improvement to the original translation."
    }'::jsonb,
    '2024-02',
    now(),
    'NAATI Certified Translator Assessment Rubrics(2024年2月最终版)',
    now()
);

-- --- Task B: Error categorisation -----------------------------------------
INSERT INTO assessment_dimensions (
    exam_type_id, dimension_key, dimension_name, scale_type, pass_threshold,
    applicable_task_type, level_descriptions, rubric_version, effective_from,
    source_reference, verified_at
) VALUES (
    '11111111-1111-1111-1111-111111111111',
    'error_categorisation',
    'Error categorisation',
    'band_1_5',
    'Band 3 or above',
    'B',
    '{
        "1": "An acceptable category is proposed for all or almost all correctly identified errors.",
        "2": "An acceptable category is proposed for most correctly identified errors.",
        "3": "An acceptable category is proposed for some correctly identified errors.",
        "4": "An acceptable category is proposed for a few correctly identified errors.",
        "5": "An acceptable category is proposed for no or almost no correctly identified errors."
    }'::jsonb,
    '2024-02',
    now(),
    'NAATI Certified Translator Assessment Rubrics(2024年2月最终版)',
    now()
);

-- =====================================================================
-- 三、error_taxonomies(对应第六节:8类错误类别)
-- example_cases先留空,按第十节的设计原则,应在实际批改中遇到边界案例后
-- 再补充few-shot示例,不在此处凭空编造
-- =====================================================================

INSERT INTO error_taxonomies (exam_type_id, category_key, category_name, description) VALUES
('11111111-1111-1111-1111-111111111111', 'distortion',              '扭曲 (Distortion)',              '译文歪曲了原文的含义或意图,导致读者获得与原文不符的信息。'),
('11111111-1111-1111-1111-111111111111', 'unjustified_omission',    '无理由省略 (Unjustified omission)', '原文中的信息在译文中被遗漏,且这种省略没有合理依据(不同于合理的意译或精简)。'),
('11111111-1111-1111-1111-111111111111', 'unjustified_addition',    '无理由增添 (Unjustified addition)', '译文中出现原文没有明确表述的信息、修饰语或因果/逻辑关系,包括看似合理但原文未支持的推断。'),
('11111111-1111-1111-1111-111111111111', 'inappropriate_register',  '语域不当 (Inappropriate register)', '译文的正式程度、语气或文体与原文的体裁、目标受众不匹配。'),
('11111111-1111-1111-1111-111111111111', 'unidiomatic_expression',  '表达不地道 (Unidiomatic expression)', '译文语义准确但中文表达生硬、欧化,不符合目标语言的自然表达习惯。'),
('11111111-1111-1111-1111-111111111111', 'grammar_syntax_error',    '语法/句法错误',                  '译文存在语法结构或句法层面的错误。'),
('11111111-1111-1111-1111-111111111111', 'spelling_error',          '拼写错误',                       '译文中出现文字层面的拼写/书写错误(含同音错字、重复字)。'),
('11111111-1111-1111-1111-111111111111', 'punctuation_error',       '标点错误',                       '译文标点符号使用不当,包括错用、漏用中文标点规范。');

-- =====================================================================
-- 四、generation_policy(对应第二节:出题难度分配比例)
-- =====================================================================

INSERT INTO generation_policy (exam_type_id, policy_key, policy_value) VALUES
(
    '11111111-1111-1111-1111-111111111111',
    'difficulty_distribution',
    '{"easy": 0.3, "medium": 0.5, "hard": 0.2}'::jsonb
),
(
    '11111111-1111-1111-1111-111111111111',
    'weak_point_targeting_ratio',
    '{"weak_point_ratio": 0.3, "random_ratio": 0.7}'::jsonb
);

-- =====================================================================
-- 五、prompt_templates
-- 拆成两层:
--   shared_methodology(subject_category='translation'):
--     怎么批改才叫严谨,是"翻译考试"这个学科通用的方法论,跟NAATI CT
--     还是以后可能做的其他翻译考试都用得上(对应第五、七、十三节)
--   exam_specific(exam_type_id指向NAATI CT英译中):
--     出题要求、Task B执行方式、输出格式,这些是NAATI CT这个具体考试
--     类型专属的(对应第一、二、三节Task B执行方式、第八、九、十、十一节)
-- =====================================================================

-- --- 5.1 shared_methodology / grading:批改的严谨度要求(第五、七、十三节) ---
INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    NULL,
    'translation',
    'grading',
    'shared_methodology',
    $tpl$
【评分关键原则】
1. 判断Meaning transfer和Language proficiency是否致命,核心标准是是否达到"impact the core message"或"impact the understanding of the target text"这个质变点。
2. 官方Band 3明确包含"taken together, have a significant impact"条款——即使单个问题都不严重,多处轻微/中等问题的累积密度本身也可能构成不达标的独立理由,不能因"没有一处特别致命"就判定安全过关。
3. Application of textual norms and conventions需综合判断是否达到"mostly appropriate/consistent"(Band 2)还是仅"some demonstrated ability...appropriate...consistent"(Band 3)。
4. 凡是"无中生有地引入具体专有名词、历史事件、比例数字"这类捏造细节的错误,一律按高优先级严重问题评估。
5. 同一类错误在同一篇译文中重复出现,须明确指出这是"系统性重复错误"而非孤立笔误,并在严重度评估中加权。
6. 每次评判先定位问题落在哪个Band区间(对照完整五档英文原文),再据此给出该维度的具体Band等级,同时明确评估"累积密度"是否本身构成风险。

【核对严谨度的强制性要求】
1. 提供参考译文时,须套用与批改用户译文完全相同的核查流程和颗粒度,写完后视为待批改译文自查一遍。若为流畅度选用了语义不完全对等的简化词,须主动识别并说明这是简化处理。
2. 介词/习语结构不预设其对应中文字面结构成立:
   - "from A to B"用于纯枚举、不暗示关联/过渡时,不可机械对应"从……到……"
   - 同样原则适用于"with little warning""down the road""outside X"
   - "over + 时间":带the past/the last/recent等限定词时表示"贯穿/在这段时间内";无此类限定接纯数量时才是"超过"
   - "less A than B"等比较结构只表达程度侧重,不可具体化为原文没有的比例/数字
3. 并列结构须先划出完整语法树,确认哪些成分真正共享同一个并列连词或介词,注意"共享修饰语"现象(A or B of X)。
4. 副词修饰范围以贴近它的动词/成分为准,不要跨越并列连词修饰更远的成分。
5. 句尾分词短语状语(尤其含"years of"等时间跨度词):判断是伴随动作还是背景/原因,若为背景/原因应提到中文句首处理。
6. 完成后自查是否遗漏"看似流畅但实际有精度问题"的地方,包括细微的无理由增删(如"似乎""社会""自己"等)。
7. 生词/易混淆词须说明词典本义与语境引申义的区别,以及最终译法是完全对应、部分对应还是简化处理。
8. 区分"翻译准确性问题"和"中文表达优化问题"——语义准确但不够自然的不应直接判错;流畅但存在精度问题的必须指出。
9. 遇到高度复杂的长难句主动提供结构拆解辅助,不要直接要求给出完整译文。

【评卷者自我纠正的方法论(必须持续遵守)】
1. 不要用"考官式扣分理由"把风格精度问题过度拔高成"严重逻辑偏离",但也不要对捏造细节或核心术语方向性误译判断过轻。
2. 不要想当然认为某维度"通过线数字更高=要求更严格",须核对该Band等级具体英文描述。
3. 警惕并识别欧化中文结构:"对……的+名词"、生硬被动直译、让步状语生硬前置/后置、抽象名词化搭配。
4. 警惕自己在提供参考译文时无理由增添"看似合理"的修饰词、语气词、因果/关联关系,或捏造细节。
5. 参考译文不是绝对正确答案,需要接受同等严格、同等颗粒度的自查。
6. 术语解释要说明专业语境原意,承认为可读性做简化是合理的,但须明确告知这是简化而非精确对应。
7. 不要假设某个语法结构的修饰/并列关系"显而易见",主动先做语法树拆解再下结论。
8. 数字类错误一律按纸面呈现结果评判严重程度。
9. 首次给出的严重度判断如经追问重新审视后发现证据不足或过轻,应坦诚上调/下调。
10. 用户独立提交的练习材料同样按完整标准评估,不因缺少Translation Brief而降低评判严谨度。
11. 同一类错误在全篇多次出现时,须明确标注为"系统性重复错误"。

请依据以下评分维度对译文进行评判:
{{ for dim in dimensions }}
### {{ dim.dimension_name }}(通过线: {{ dim.pass_threshold }})
{{ for band in dim.level_descriptions }}
Band {{ band.key }}: {{ band.value }}
{{ end }}
{{ end }}

请依据以下错误类别对发现的问题分类:
{{ for cat in error_taxonomies }}
- {{ cat.category_key }}({{ cat.category_name }}): {{ cat.description }}
{{ end }}

评判要零容忍,标题、标点、语法、句子结构、逻辑关系逐句逐词核查,明确指出每处错误所属评分维度、大致Band区间、是否影响核心信息或理解。
$tpl$,
    1,
    TRUE
);

-- --- 5.2 shared_methodology / grading:输出格式与结论要求(第十、十一节) ---
INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    NULL,
    'translation',
    'grading',
    'shared_methodology',
    $tpl$
【输出格式】
每次批改包含四部分:
1. 错误清单表:位置/原文-译文对照/错误类别/所属评分维度/是否影响核心信息或理解/说明/建议
2. 原文高频词汇/短语/固定搭配/习语表:基于原文本身列出,须覆盖标题、习语、转喻表达、易被直译错的介词/时间结构
3. 典型句型识别与拆解
4. 长难句结构地图(1-2处最复杂句子)

参考译文中一律不使用破折号,改用括号或逗号+同位语处理插入说明。

【长难句与句型积累】(即使译文没有错误也应正常提供)
1. 典型句型识别与拆解:标注句型名称、原文例句、拆解步骤(主干→从句/同位语/插入语→组装)、该句型可能出现的变体
2. 长难句结构地图:对最复杂的1-2处长难句提供逐层拆解图示,标注每层在中文里应如何转换语序

【常用表达积累要求】
- 优先记录整体用法而非拆分单词,如enable someone to do、be subject to、in the wake of
- 区分词典本义、常见引申义、当前语境义
- 习语须说明是否可直译,不可机械直译的须明确标记
- 优先积累易混淆近义词(enable vs help、assume vs presume、eventually vs finally)
- 高频介词(over、by、with、through、within、outside、beyond、against)须积累不同语境下的实际功能

【评估结论输出要求】
评估最后须给出:
(a) 各维度具体Band等级判断,简要引用对应英文Band描述作为依据
(b) 明确评估错误的"累积密度"是否构成独立的降级风险
(c) 主观估算的"本篇译文通过概率"(明确注明非官方数据)
$tpl$,
    1,
    TRUE
);

-- --- 5.3 exam_specific / question_gen:出题要求(第一、二节) ---
INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    '11111111-1111-1111-1111-111111111111',
    NULL,
    'question_gen',
    'exam_specific',
    $tpl$
你是NAATI CT(Certified Translator,英译中方向)考试的出题员。

【任务模式】
- 出一篇英译中翻译任务(约250词),含Translation Brief(领域/文本类型/目的/受众)和标题
- 尽可能使用真实存在的文章,不要凭空捏造
- 出题应符合NAATI出题风格,多偏向澳大利亚各类场景
- 专业词汇要准确但不刁钻;论证以线性事实/数据推进为主;可适当包含机构名、期刊名、人名

【难度分层系统】(官方定位为"complex but non-specialised")
- 简单档:长难句1-2处,多为单层分词状语或简单定语从句;论证结构线性;术语密度低
- 中等档:长难句2-3处,允许1-2层嵌套;可能含直接引语及说话人身份的复杂修饰;含1-3个需精确处理的专业/政策术语;论证可能含一次转折/对比
- 困难档:长难句3-4处,允许多层嵌套(3层以上);抽象名词堆叠句式;术语密度较高;篇章结构更依赖上下文推理;主句成分可能被大幅后置

本次出题难度档位:{{ difficulty }}

在Translation Brief中明确标注本次难度档位。
$tpl$,
    1,
    TRUE
);

-- --- 5.4 exam_specific / grading:Task类型与通过标准说明(第三节) ---
INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    '11111111-1111-1111-1111-111111111111',
    NULL,
    'grading',
    'exam_specific',
    $tpl$
本次评判任务类型:{{ task_type }}

Task A(非专业文本翻译)三个独立维度,通过线:
1. Meaning transfer —— Pass: Band 2 or above
2. Application of textual norms and conventions —— Pass: Band 3 or above
3. Language proficiency —— Pass: Band 2 or above

Task B(非专业译文审校)两个独立维度,通过线:
1. Revision skills —— Pass: Band 2 or above
2. Error categorisation —— Pass: Band 3 or above

注意:不同维度的"通过线Band数字"不能直接互相比较严格程度,必须分别参照各自完整的Band等级英文原文描述判断。

Task B执行方式:给出完整英文原文和含多处预设错误的完整译文,一次性全部提供,由用户独立通读、找错、归类、更正后一次性提交,再核对识别率与归类准确率。
$tpl$,
    1,
    TRUE
);

-- --- 5.5 exam_specific / followup:追问处理原则 ---
INSERT INTO prompt_templates (
    exam_type_id, subject_category, template_type, layer, template_content, version, is_active
) VALUES (
    '11111111-1111-1111-1111-111111111111',
    NULL,
    'followup',
    'exam_specific',
    $tpl$
用户对以下评判结果提出了疑问:
{{ followup_question_text }}

请重新审视原始判断依据(对照assessment_dimensions的Band英文原文与error_taxonomies定义),判断用户的观点是否成立。
如果首次给出的严重度判断证据不足或过轻,应坦诚上调/下调,并说明修正后的推理过程。
不要因为用户情绪化的申诉而降低标准,也不要因为担心冲突而回避明确表态。
最终请给出verdict:user_correct / user_incorrect / partial,并说明理由。
$tpl$,
    1,
    TRUE
);

COMMIT;

-- =====================================================================
-- 六、weak_points 种子数据(对应第十二节:个人薄弱点清单)
-- 这不是prompt模板内容,是要结构化追踪的用户数据。
-- 使用前请把下面'YOUR_USERNAME'替换成你在users表里的真实username。
-- =====================================================================

-- BEGIN;
--
-- INSERT INTO weak_points (user_id, category, description, status, priority) VALUES
-- ((SELECT id FROM users WHERE username = 'YOUR_USERNAME'), '省略保留性/让步语气词', '倾向把some/roughly/likely/a few/up to/well before/closer to/more than等不确定表述译得过于肯定或丢失程度限定', 'active', 'high'),
-- ((SELECT id FROM users WHERE username = 'YOUR_USERNAME'), '被动语态处理不稳', '要么漏掉被动标记导致主宾颠倒,要么生硬直译"被要求/被建议"这类翻译腔', 'active', 'medium'),
-- ((SELECT id FROM users WHERE username = 'YOUR_USERNAME'), '数字/统计类陷阱', '倍数换算(double≠翻两倍)、百分比修饰对象、程度副词精确对应、近似值精确化、数字单位被错误区间化、over+时间两种含义混淆', 'active', 'high'),
-- ((SELECT id FROM users WHERE username = 'YOUR_USERNAME'), '逻辑关系还原', '因果方向颠倒、修饰语错位、同位说明被误处理成因果关系、并列结构被合并、共享修饰语被误判、让步转折重心弄反', 'active', 'high'),
-- ((SELECT id FROM users WHERE username = 'YOUR_USERNAME'), '专业/职业名词层级混淆', '如paralegal≠lawyer、bookkeeper≠accountant、linguistics department≠语言系;固定商业/学术术语被泛化处理', 'active', 'medium'),
-- ((SELECT id FROM users WHERE username = 'YOUR_USERNAME'), '完成度硬伤', '未完成翻译痕迹、语法杂糅读不通、同音错字/重复字导致句子不通', 'active', 'medium'),
-- ((SELECT id FROM users WHERE username = 'YOUR_USERNAME'), '术语前后不一致', '同一英文词在同一篇译文中用了不同中文对应词,或同一误译在全篇重复出现', 'active', 'medium'),
-- ((SELECT id FROM users WHERE username = 'YOUR_USERNAME'), '困难档长难句卡壳', '面对困难档长难句不知从何下手,需先做结构拆解', 'active', 'low'),
-- ((SELECT id FROM users WHERE username = 'YOUR_USERNAME'), '无中生有具体化', '将比较/程度结构具体化为捏造的比例数字,或将泛指表达替换为具体专有名词', 'active', 'medium'),
-- ((SELECT id FROM users WHERE username = 'YOUR_USERNAME'), '形近/义近词混淆', '如at times≠it''s time,enable≠help,容易造成方向性误判', 'active', 'medium');
--
-- COMMIT;
