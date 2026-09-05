-- =====================================================================
-- 薄弱点两级分类体系 — 二级叶子种子数据(全局共享,不按 exam_type 区分)
-- 来源:薄弱点分类与生命周期管理_策划书.md §1.1
--
-- 依赖:先跑 seed_weak_point_categories.sql(本文件用 category code 反查
--       category_id)。
--
-- default_dimension_key / default_error_category 留空:这套叶子是通用语言学
-- 范畴,不与某个具体考试类型的评分维度绑定,交由 AI 归类(weak_point_classification)
-- 判断,不走旧的按维度规则兜底路径。
--
-- 手动执行(Supabase SQL Editor 或 psql)。幂等:ON CONFLICT DO NOTHING。
-- =====================================================================

BEGIN;

INSERT INTO weak_point_catalog (category_id, code, name, description)
VALUES
    -- Semantic Errors / 语义错误
    ((SELECT id FROM weak_point_categories WHERE code = 'semantic_errors'),
     'semantic_subject_object', 'Subject-Object Relationship / 主客关系',
     '谁对谁做了什么被弄反或弄混,施事与受事关系传达错误。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'semantic_errors'),
     'semantic_negation', 'Negation / 否定',
     '否定范围、否定对象或双重否定被误处理,肯定/否定意义传达反了。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'semantic_errors'),
     'semantic_modality', 'Modality / 情态',
     '情态动词或情态副词表达的可能性、必要性、意愿等程度被译得过强或过弱。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'semantic_errors'),
     'semantic_causality', 'Causality / 因果',
     '原因与结果的方向被颠倒,或把非因果的并列/同位关系误加上因果标记。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'semantic_errors'),
     'semantic_temporal', 'Temporal Relation / 时间关系',
     '时间先后顺序、时间点与时间段的区分、或时间状语的修饰范围被译错。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'semantic_errors'),
     'semantic_spatial', 'Spatial Relation / 空间关系',
     '方位、方向或空间归属关系被误传达。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'semantic_errors'),
     'semantic_comparison', 'Comparison / 比较',
     '比较的对象、方向(更多/更少)或比较基准被弄错或丢失。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'semantic_errors'),
     'semantic_scope', 'Scope / 范围',
     '修饰语、限定词或否定词的辖域(管到哪里为止)被误判,导致语义范围扩大或缩小。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'semantic_errors'),
     'semantic_reference', 'Reference / 指代',
     '代词、指示词或省略的先行词被指代错误的对象。'),

    -- Lexical Errors / 词汇错误
    ((SELECT id FROM weak_point_categories WHERE code = 'lexical_errors'),
     'lexical_wrong_word_choice', 'Wrong Word Choice / 词汇选择',
     '选用了语义不贴切或感情色彩不符的词,常因形近/义近词混淆导致方向性误判。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'lexical_errors'),
     'lexical_terminology', 'Terminology / 术语',
     '专业术语被泛化或替换成层级不对应的近义词(如职业头衔、学科名称混淆)。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'lexical_errors'),
     'lexical_entity', 'Entity / 实体',
     '人名、地名、机构名等专有名词被译错、误代换或无中生有。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'lexical_errors'),
     'lexical_polysemy', 'Polysemy / 多义词',
     '一词多义的词选用了不符合当前语境的义项。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'lexical_errors'),
     'lexical_collocation', 'Collocation / 搭配',
     '词语单独看没错,但组合搭配不符合目标语言的惯用表达。'),

    -- Grammatical / Syntactic Errors / 语法句法错误
    ((SELECT id FROM weak_point_categories WHERE code = 'grammatical_syntactic_errors'),
     'grammar_tense_aspect', 'Tense / Aspect / 时态体',
     '动作发生的时间或进行/完成状态被译错。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'grammatical_syntactic_errors'),
     'grammar_voice', 'Voice / 语态',
     '被动/主动语态处理不稳,漏掉被动标记导致主宾颠倒,或生硬直译出翻译腔。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'grammatical_syntactic_errors'),
     'grammar_number', 'Number / 单复数',
     '单复数信息丢失或译错,导致数量含义不准确。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'grammatical_syntactic_errors'),
     'grammar_person', 'Person / 人称',
     '人称(第一/二/三人称)被弄混,导致叙述视角或归属对象错误。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'grammatical_syntactic_errors'),
     'grammar_word_order', 'Word Order / 语序',
     '目标语言语序调整不当,造成阅读困难或修饰关系模糊。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'grammatical_syntactic_errors'),
     'grammar_modifier_attachment', 'Modifier / Attachment / 修饰关系',
     '修饰语、从句或插入语的依附对象被判断错误,常见于长难句结构拆解不到位。'),

    -- Information Errors / 信息错误
    ((SELECT id FROM weak_point_categories WHERE code = 'information_errors'),
     'info_omission', 'Omission / 漏译',
     '原文信息(尤其是程度、限定或让步表述)未经说明理由地被漏译或整体丢失。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'information_errors'),
     'info_addition', 'Addition / 增译',
     '译文加入了原文没有的具体数字、专名或情节,无中生有地具体化。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'information_errors'),
     'info_mistranslation', 'Mistranslation / 误译',
     '对原文意思的整体理解出现偏差,译出与原意无关或相反的内容。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'information_errors'),
     'info_distortion', 'Distortion / 语义偏移',
     '倍数、百分比、约数等数值或程度表达被精确化、放大或缩小,偏离原文实际含义。'),

    -- Discourse / Logic Errors / 篇章逻辑错误
    ((SELECT id FROM weak_point_categories WHERE code = 'discourse_logic_errors'),
     'discourse_conjunction', 'Conjunction / 连接关系',
     '连接词所表达的逻辑关系(并列/转折/递进等)被误换成另一种关系。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'discourse_logic_errors'),
     'discourse_cohesion', 'Cohesion / 衔接',
     '句间衔接手段(代词复现、省略、连接词)处理不当,导致译文各句显得孤立。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'discourse_logic_errors'),
     'discourse_coherence', 'Coherence / 连贯性',
     '译文各部分逻辑走向与原文整体论述脉络不一致。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'discourse_logic_errors'),
     'discourse_consistency', 'Consistency / 一致性',
     '同一原文用词在全篇译文中前后不一致(术语、译名或重复错误未统一)。'),

    -- Pragmatic / Stylistic Errors / 语用风格错误
    ((SELECT id FROM weak_point_categories WHERE code = 'pragmatic_stylistic_errors'),
     'pragmatic_register', 'Register / 语体',
     '译文的正式/非正式程度与原文场合不符(如公文译成口语,或反之)。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'pragmatic_stylistic_errors'),
     'pragmatic_tone', 'Tone / 语气',
     '原文的委婉、强调、讽刺等语气在译文中被削弱、放大或消失。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'pragmatic_stylistic_errors'),
     'pragmatic_pragmatics', 'Pragmatics / 语用',
     '字面意思译对了,但言外之意、隐含前提或语用功能未能传达。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'pragmatic_stylistic_errors'),
     'pragmatic_cultural_localization', 'Cultural / Localization / 文化本地化',
     '文化专属概念、习俗或本地化表达处理不当,直译导致目标读者难以理解。'),

    -- Fluency / Naturalness / 表达质量
    ((SELECT id FROM weak_point_categories WHERE code = 'fluency_naturalness'),
     'fluency_smoothness', 'Fluency / 流畅性',
     '译文读起来断续、卡顿,句子内部或句间过渡不顺畅。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'fluency_naturalness'),
     'fluency_naturalness_leaf', 'Naturalness / 自然度',
     '译文虽然语法正确,但表达方式不符合目标语言母语者的惯常说法。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'fluency_naturalness'),
     'fluency_awkward_expression', 'Awkward Expression / 生硬表达',
     '存在明显的翻译腔、生造词或读不通的杂糅表达,影响可读性。'),

    -- Mechanics / 语言形式
    ((SELECT id FROM weak_point_categories WHERE code = 'mechanics'),
     'mechanics_punctuation', 'Punctuation / 标点',
     '标点符号使用不符合目标语言规范,或误用原文语言的标点习惯。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'mechanics'),
     'mechanics_formatting', 'Formatting / 格式',
     '段落、编号、排版等格式呈现与原文或规范要求不符。'),
    ((SELECT id FROM weak_point_categories WHERE code = 'mechanics'),
     'mechanics_number_date_unit', 'Number / Date / Unit / 数字日期单位',
     '数字、日期或计量单位的换算、格式或精确度处理错误。')
ON CONFLICT (code) DO NOTHING;

COMMIT;

-- 验证:SELECT count(*) FROM weak_point_catalog WHERE origin = 'seed' AND category_id IS NOT NULL;  -- 应为 38
