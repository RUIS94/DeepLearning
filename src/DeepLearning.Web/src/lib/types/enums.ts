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
  // 后端 SubmissionStatus.cs 里 regraded 追加在末尾（=9），不是插在中间——见那里的注释。
  // score_challenge 追问 decision=adjust 后：那一个维度的 Band 已被改写、留有 GradingResultRevision 审计行。
  regraded: 9,
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
  score_challenge_summary: 10,
  vocab_semantic_drift: 11,
} as const;

// 追问线程（design decision, 2026-09-02）：一个 submission 最多一条线程，存续期间
// submission 停在 under_dispute，用户点“结束追问”才结算——见 FollowUpThread.cs 的注释。
export const FollowUpThreadStatus = { open: 0, closed: 1 } as const;
export const FollowUpMessageRole = { user: 0, ai: 1 } as const;
// 追问线程的种类（后端 FollowUpThreadKind）。knowledge = 纯知识问答；dispute = 质疑某条评判；
// score_challenge = 从分数区对某个维度的 Band 发起改判申请（结算时可真正改分）。
export const FollowUpThreadKind = { knowledge: 0, dispute: 1, score_challenge: 2 } as const;
export const FollowUpThreadKindLabel: Record<number, string> = {
  [FollowUpThreadKind.knowledge]: "知识追问",
  [FollowUpThreadKind.dispute]: "评判质疑",
  [FollowUpThreadKind.score_challenge]: "分数改判申请",
};
// score_challenge_summary 的 decision（后端 ScoreChallengeDecision）——JSON 里是字符串，不是序数。
export const ScoreChallengeDecision = { uphold: "uphold", adjust: "adjust" } as const;

export const TaskTypeLabel: Record<number, string> = {
  [TaskType.A]: "TaskA",
  [TaskType.B]: "TaskB",
};

export const DifficultyLabel: Record<number, string> = {
  [Difficulty.easy]: "Simple",
  [Difficulty.medium]: "Medium",
  [Difficulty.hard]: "Hard",
};

export const SubmissionStatusLabel: Record<number, string> = {
  [SubmissionStatus.draft]: "Draft",
  [SubmissionStatus.submitted]: "Submitted",
  [SubmissionStatus.grading]: "Grading",
  [SubmissionStatus.grading_failed]: "Grading Failed",
  [SubmissionStatus.graded]: "Graded",
  [SubmissionStatus.under_dispute]: "Under Dispute",
  [SubmissionStatus.standard_revised]: "Standard Revised",
  [SubmissionStatus.archived]: "Archived",
  [SubmissionStatus.grading_abandoned]: "Grading Abandoned",
  [SubmissionStatus.regraded]: "Regarded",
};

export const FollowUpVerdictLabel: Record<number, string> = {
  [FollowUpVerdict.user_correct]: "User Correct",
  [FollowUpVerdict.user_incorrect]: "User Incorrect",
  [FollowUpVerdict.partial]: "Partial",
  [FollowUpVerdict.pending]: "Pending",
};

export const OverrideStatusLabel: Record<number, string> = {
  [OverrideStatus.observing]: "Observing",
  [OverrideStatus.active]: "Active",
  [OverrideStatus.deprecated]: "Deprecated",
};

export const WeakPointCatalogStatusLabel: Record<number, string> = {
  [WeakPointCatalogStatus.proposed]: "Proposed",
  [WeakPointCatalogStatus.active]: "Active",
  [WeakPointCatalogStatus.deprecated]: "Deprecated",
};

export const WeakPointStatusLabel: Record<number, string> = {
  [WeakPointStatus.active]: "Active",
  [WeakPointStatus.resolved]: "Resolved",
  [WeakPointStatus.tracking]: "Tracking",
};

export const PriorityLabel: Record<number, string> = {
  [Priority.high]: "High",
  [Priority.medium]: "Medium",
  [Priority.low]: "Low",
};

export const MasteryLevelLabel: Record<number, string> = {
  [MasteryLevel.New]: "New",
  [MasteryLevel.Familiar]: "Familiar",
  [MasteryLevel.Mastered]: "Mastered",
};

export const SubjectCategoryLabel: Record<number, string> = {
  [SubjectCategory.translation]: "Translation",
  [SubjectCategory.language_arts]: "Language Arts",
  [SubjectCategory.math]: "Math",
  [SubjectCategory.science]: "Science",
  [SubjectCategory.other]: "Other",
};

export const TemplateLayerLabel: Record<number, string> = {
  [TemplateLayer.shared_methodology]: "Shared Methodology",
  [TemplateLayer.exam_specific]: "Exam-Specific",
};

export const CheckpointImportanceLabel: Record<number, string> = {
  [CheckpointImportance.core]: "Core",
  [CheckpointImportance.peripheral]: "Peripheral",
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
  [AiOperationType.question_gen]: "Question Generation",
  [AiOperationType.grading]: "Grading",
  [AiOperationType.followup]: "Follow-up",
  [AiOperationType.standard_revision]: "Standard Revision",
  [AiOperationType.deep_learning]: "Deep Learning",
  [AiOperationType.progress_trend]: "Progress Trend",
  [AiOperationType.followup_summary]: "Follow-up Summary",
  [AiOperationType.weak_point_classification]: "Weak Point Classification",
  [AiOperationType.weak_point_detection_criteria]: "Weak Point Detection Criteria",
  [AiOperationType.weak_point_recheck]: "Weak Point Recheck",
  [AiOperationType.score_challenge_summary]: "Score Challenge Summary",
  [AiOperationType.vocab_semantic_drift]: "Vocabulary Semantic Drift",
};

export const FollowUpThreadStatusLabel: Record<number, string> = {
  [FollowUpThreadStatus.open]: "Follow-up in Progress",
  [FollowUpThreadStatus.closed]: "Closed",
};

export const ScaleTypeLabel: Record<number, string> = {
  [ScaleType.band_1_5]: "Band 1-5",
  [ScaleType.score_0_100]: "Percentage Scale",
  [ScaleType.rubric_level]: "Rubric Level",
};

export const CategoryTypeLabel: Record<number, string> = {
  [CategoryType.domain]: "Domain",
  [CategoryType.scenario]: "Scenario",
};
