// 与后端契约一致：枚举在 JSON 中是数字序数（见方案 3.3 / 6.2 节）

export const TaskType = { A: 0, B: 1 } as const;
export const Difficulty = { easy: 0, medium: 1, hard: 2 } as const;
export const QuestionOrigin = { ai_generated: 0, user_uploaded: 1, real_exam_seed: 2 } as const;
export const SourceType = { real_exam: 0, ai_generated: 1, user_generated: 2 } as const;
export const Visibility = { Private: 0, Shared: 1 } as const;
export const SubmissionStatus = {
  draft: 0,
  submitted: 1,
  grading: 2,
  grading_failed: 3,
  graded: 4,
  under_dispute: 5,
  standard_revised: 6,
  archived: 7,
  grading_abandoned: 8,
} as const;
export const FollowUpVerdict = {
  user_correct: 0,
  user_incorrect: 1,
  partial: 2,
  pending: 3,
} as const;
/**
 * 评判之后的薄弱点生成进度（后端 WeakPointGenerationStatus）。null 表示"不适用"——没评判过，
 * 或者是这个字段上线前就已经评完的旧记录，此时前端什么标签都不显示。
 */
export const WeakPointGenerationStatus = {
  pending: 0,
  running: 1,
  succeeded: 2,
  failed: 3,
} as const;

export const WeakPointGenerationStatusLabel: Record<number, string> = {
  [WeakPointGenerationStatus.pending]: "薄弱点待生成",
  [WeakPointGenerationStatus.running]: "正在生成薄弱点",
  [WeakPointGenerationStatus.succeeded]: "薄弱点已生成",
  [WeakPointGenerationStatus.failed]: "薄弱点生成失败",
};

export const OverrideScope = { grading_rubric: 0, translation_reference: 1 } as const;
export const OverrideStatus = { observing: 0, active: 1, deprecated: 2 } as const;
// 薄弱点种类生命周期。proposed = 运行期/后台新建待审;active = 已策展;deprecated = 合并后退役。
export const WeakPointCatalogStatus = { proposed: 0, active: 1, deprecated: 2 } as const;
// tracking 追加在最后（=2），不是插在中间——后端枚举没有 JsonStringEnumConverter，
// 序列化成裸序数，插在中间会把已有的 active=0/resolved=1 错位（见后端 WeakPointStatus.cs 的注释）。
// tracking = 首次命中、还没攒够 3 次提交确认，不会出现在评分提示词里，也不参与复核。
export const WeakPointStatus = { active: 0, resolved: 1, tracking: 2 } as const;
// 注意：high = 0，排序时不要写反
export const Priority = { high: 0, medium: 1, low: 2 } as const;
export const ScaleType = { band_1_5: 0, score_0_100: 1, rubric_level: 2 } as const;
export const CategoryType = { domain: 0, scenario: 1 } as const;
export const MasteryLevel = { New: 0, Familiar: 1, Mastered: 2 } as const;
export const SubjectCategory = {
  translation: 0,
  language_arts: 1,
  math: 2,
  science: 3,
  other: 4,
} as const;
export const TemplateLayer = { shared_methodology: 0, exam_specific: 1 } as const;
export const CheckpointImportance = { core: 0, peripheral: 1 } as const;
// 每条评分错误的严重程度。后端 error_severity_enum。前端"影响核心/接近边界/非核心"
// 标签由它派生（见 errorImpactLabel），不是后端单独字段。
/**
 * NAATI 官方只分两级：Major = 影响意图/功能，或影响读者理解；Minor = 只有精度损失。
 * 原来还有 moderate / critical 两个本项目自造的中间档，55 条真实结果里 40 moderate、14 major、
 * 1 critical、minor 一条都没有——四档当两档用，而且两个落点都不在官方定义的位置上。
 */
export const ErrorSeverity = { minor: 0, major: 1 } as const;
// 已对照 Domain/Enums/AiOperationType.cs 核实：prompt_templates.template_type 实际存的是
// 这个 6 员枚举，不是只有 question_gen/grading/followup/standard_revision 4 个值——
// deep_learning/progress_trend 两个模板类型在 Supabase 里已经有真实行（AGENTS.md 有记录），
// 之前这里只声明了 4 个值会导致这两类模板在 admin 后台完全无法选中/正确展示。
export const AiOperationType = {
  question_gen: 0,
  grading: 1,
  followup: 2,
  standard_revision: 3,
  deep_learning: 4,
  progress_trend: 5,
  followup_summary: 6,
  weak_point_classification: 7,
  weak_point_detection_criteria: 8,
  weak_point_recheck: 9,
} as const;

// 追问线程（design decision, 2026-09-02）：一个 submission 最多一条线程，存续期间
// submission 停在 under_dispute，用户点“结束追问”才结算——见 FollowUpThread.cs 的注释。
export const FollowUpThreadStatus = { open: 0, closed: 1 } as const;
export const FollowUpMessageRole = { user: 0, ai: 1 } as const;

export const TaskTypeLabel: Record<number, string> = {
  [TaskType.A]: "TaskA · 翻译",
  [TaskType.B]: "TaskB · 找错标注",
};

export const DifficultyLabel: Record<number, string> = {
  [Difficulty.easy]: "简单",
  [Difficulty.medium]: "中等",
  [Difficulty.hard]: "困难",
};

export const SubmissionStatusLabel: Record<number, string> = {
  [SubmissionStatus.draft]: "草稿",
  [SubmissionStatus.submitted]: "已提交",
  [SubmissionStatus.grading]: "批改中",
  [SubmissionStatus.grading_failed]: "批改失败",
  [SubmissionStatus.graded]: "已批改",
  [SubmissionStatus.under_dispute]: "追问中",
  [SubmissionStatus.standard_revised]: "标准已修订",
  [SubmissionStatus.archived]: "已归档",
  [SubmissionStatus.grading_abandoned]: "已放弃批改",
};

export const FollowUpVerdictLabel: Record<number, string> = {
  [FollowUpVerdict.user_correct]: "用户判断正确",
  [FollowUpVerdict.user_incorrect]: "维持原判",
  [FollowUpVerdict.partial]: "部分成立",
  [FollowUpVerdict.pending]: "处理中",
};

export const OverrideStatusLabel: Record<number, string> = {
  [OverrideStatus.observing]: "观察中",
  [OverrideStatus.active]: "已生效",
  [OverrideStatus.deprecated]: "已废弃",
};

export const WeakPointCatalogStatusLabel: Record<number, string> = {
  [WeakPointCatalogStatus.proposed]: "待审",
  [WeakPointCatalogStatus.active]: "已启用",
  [WeakPointCatalogStatus.deprecated]: "已退役",
};

export const WeakPointStatusLabel: Record<number, string> = {
  [WeakPointStatus.active]: "活跃",
  [WeakPointStatus.resolved]: "已改善",
  [WeakPointStatus.tracking]: "观察中",
};

export const PriorityLabel: Record<number, string> = {
  [Priority.high]: "高",
  [Priority.medium]: "中",
  [Priority.low]: "低",
};

export const MasteryLevelLabel: Record<number, string> = {
  [MasteryLevel.New]: "新接触",
  [MasteryLevel.Familiar]: "熟悉",
  [MasteryLevel.Mastered]: "已掌握",
};

export const SubjectCategoryLabel: Record<number, string> = {
  [SubjectCategory.translation]: "翻译",
  [SubjectCategory.language_arts]: "语言文学",
  [SubjectCategory.math]: "数学",
  [SubjectCategory.science]: "科学",
  [SubjectCategory.other]: "其他",
};

export const TemplateLayerLabel: Record<number, string> = {
  [TemplateLayer.shared_methodology]: "共享方法论",
  [TemplateLayer.exam_specific]: "考试类型专属",
};

export const CheckpointImportanceLabel: Record<number, string> = {
  [CheckpointImportance.core]: "核心",
  [CheckpointImportance.peripheral]: "边缘",
};

export const ErrorSeverityLabel: Record<number, string> = {
  [ErrorSeverity.minor]: "Minor",
  [ErrorSeverity.major]: "Major",
};

/**
 * 由 severity 派生的“是否影响核心意义”标签 + 语气（旧的 impacts_core 布尔已退役）。
 * 官方 Major 的判据就是"影响意图/功能，或影响读者理解"，所以这个标签与 severity 一一对应。
 */
export function errorImpactLabel(severity: number): {
  text: string;
  tone: "danger" | "muted";
} {
  if (severity === ErrorSeverity.major) return { text: "影响理解", tone: "danger" };
  return { text: "仅精度损失", tone: "muted" };
}

export const AiOperationTypeLabel: Record<number, string> = {
  [AiOperationType.question_gen]: "出题",
  [AiOperationType.grading]: "评分",
  [AiOperationType.followup]: "追问",
  [AiOperationType.standard_revision]: "标准修订",
  [AiOperationType.deep_learning]: "深入学习",
  [AiOperationType.progress_trend]: "进度趋势",
  [AiOperationType.followup_summary]: "追问总结",
  [AiOperationType.weak_point_classification]: "薄弱点分类",
  [AiOperationType.weak_point_detection_criteria]: "薄弱点筛查标准生成",
  [AiOperationType.weak_point_recheck]: "薄弱点复核",
};

export const FollowUpThreadStatusLabel: Record<number, string> = {
  [FollowUpThreadStatus.open]: "追问中",
  [FollowUpThreadStatus.closed]: "已结束",
};

export const ScaleTypeLabel: Record<number, string> = {
  [ScaleType.band_1_5]: "Band 1-5",
  [ScaleType.score_0_100]: "百分制",
  [ScaleType.rubric_level]: "等级制",
};

export const CategoryTypeLabel: Record<number, string> = {
  [CategoryType.domain]: "领域",
  [CategoryType.scenario]: "应用场景",
};
