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
export const OverrideScope = { grading_rubric: 0, translation_reference: 1 } as const;
export const OverrideStatus = { observing: 0, active: 1, deprecated: 2 } as const;
export const WeakPointStatus = { active: 0, resolved: 1 } as const;
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
// ⚠️ 后端 Domain/Enums 里的声明顺序未逐条核对（对接后端前请核实）：
// design doc §6.24 只给了取值集合 question_gen/grading/followup/standard_revision，
// 未明确声明顺序，这里按“出题在最前”的直觉排列，接后端时务必对照源码修正。
export const TemplateType = {
  question_gen: 0,
  grading: 1,
  followup: 2,
  standard_revision: 3,
} as const;

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

export const WeakPointStatusLabel: Record<number, string> = {
  [WeakPointStatus.active]: "活跃",
  [WeakPointStatus.resolved]: "已改善",
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

export const TemplateTypeLabel: Record<number, string> = {
  [TemplateType.question_gen]: "出题",
  [TemplateType.grading]: "评分",
  [TemplateType.followup]: "追问",
  [TemplateType.standard_revision]: "标准修订",
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
