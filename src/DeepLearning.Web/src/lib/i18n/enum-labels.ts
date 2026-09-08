"use client";

/**
 * 枚举标签的中英两套映射。`@/lib/types/enums` 里的 `XxxLabel` 常量是英文原文（真相源），
 * 这里只补一套中文；`useEnumLabels()` 按当前界面语言返回对应那套。
 *
 * 用法：把 `import { DifficultyLabel } from "@/lib/types/enums"` 换成
 *   const { DifficultyLabel } = useEnumLabels();
 * 迭代（下拉选项）和按序数取值都不受影响 —— key 仍是数字序数，只有 value（展示文案）随语言变。
 */
import { useMemo } from "react";
import { useI18n } from "@/lib/i18n";
import * as en from "@/lib/types/enums";

type LabelMap = Record<number, string>;

const zh = {
  WeakPointGenerationStatusLabel: {
    [en.WeakPointGenerationStatus.pending]: "薄弱点待生成",
    [en.WeakPointGenerationStatus.running]: "正在生成薄弱点",
    [en.WeakPointGenerationStatus.succeeded]: "薄弱点已生成",
    [en.WeakPointGenerationStatus.failed]: "薄弱点生成失败",
  } as LabelMap,
  FollowUpThreadKindLabel: {
    [en.FollowUpThreadKind.knowledge]: "知识追问",
    [en.FollowUpThreadKind.dispute]: "评判质疑",
    [en.FollowUpThreadKind.score_challenge]: "分数改判",
  } as LabelMap,
  TaskTypeLabel: {
    [en.TaskType.A]: "任务A",
    [en.TaskType.B]: "任务B",
  } as LabelMap,
  DifficultyLabel: {
    [en.Difficulty.easy]: "简单",
    [en.Difficulty.medium]: "中等",
    [en.Difficulty.hard]: "困难",
  } as LabelMap,
  SubmissionStatusLabel: {
    [en.SubmissionStatus.draft]: "草稿",
    [en.SubmissionStatus.submitted]: "已提交",
    [en.SubmissionStatus.grading]: "评分中",
    [en.SubmissionStatus.grading_failed]: "评分失败",
    [en.SubmissionStatus.graded]: "已评分",
    [en.SubmissionStatus.under_dispute]: "追问中",
    [en.SubmissionStatus.standard_revised]: "标准已修订",
    [en.SubmissionStatus.archived]: "已归档",
    [en.SubmissionStatus.grading_abandoned]: "评分已放弃",
    [en.SubmissionStatus.regraded]: "已重评",
  } as LabelMap,
  FollowUpVerdictLabel: {
    [en.FollowUpVerdict.user_correct]: "用户正确",
    [en.FollowUpVerdict.user_incorrect]: "用户有误",
    [en.FollowUpVerdict.partial]: "部分正确",
    [en.FollowUpVerdict.pending]: "待定",
  } as LabelMap,
  OverrideStatusLabel: {
    [en.OverrideStatus.observing]: "观察中",
    [en.OverrideStatus.active]: "生效中",
    [en.OverrideStatus.deprecated]: "已弃用",
  } as LabelMap,
  WeakPointCatalogStatusLabel: {
    [en.WeakPointCatalogStatus.proposed]: "待审",
    [en.WeakPointCatalogStatus.active]: "已启用",
    [en.WeakPointCatalogStatus.deprecated]: "已弃用",
  } as LabelMap,
  WeakPointStatusLabel: {
    [en.WeakPointStatus.active]: "活跃",
    [en.WeakPointStatus.resolved]: "已解决",
    [en.WeakPointStatus.tracking]: "跟踪中",
  } as LabelMap,
  PriorityLabel: {
    [en.Priority.high]: "高",
    [en.Priority.medium]: "中",
    [en.Priority.low]: "低",
  } as LabelMap,
  MasteryLevelLabel: {
    [en.MasteryLevel.New]: "新",
    [en.MasteryLevel.Familiar]: "熟悉",
    [en.MasteryLevel.Mastered]: "已掌握",
  } as LabelMap,
  SubjectCategoryLabel: {
    [en.SubjectCategory.translation]: "翻译",
    [en.SubjectCategory.language_arts]: "语文",
    [en.SubjectCategory.math]: "数学",
    [en.SubjectCategory.science]: "科学",
    [en.SubjectCategory.other]: "其他",
  } as LabelMap,
  TemplateLayerLabel: {
    [en.TemplateLayer.shared_methodology]: "通用方法论",
    [en.TemplateLayer.exam_specific]: "考试专属",
  } as LabelMap,
  CheckpointImportanceLabel: {
    [en.CheckpointImportance.core]: "核心",
    [en.CheckpointImportance.peripheral]: "次要",
  } as LabelMap,
  ErrorSeverityLabel: {
    [en.ErrorSeverity.minor]: "轻微",
    [en.ErrorSeverity.major]: "严重",
  } as LabelMap,
  AiOperationTypeLabel: {
    [en.AiOperationType.question_gen]: "出题",
    [en.AiOperationType.grading]: "评分",
    [en.AiOperationType.followup]: "追问",
    [en.AiOperationType.standard_revision]: "标准修订",
    [en.AiOperationType.deep_learning]: "深入学习",
    [en.AiOperationType.progress_trend]: "进度趋势",
    [en.AiOperationType.followup_summary]: "追问总结",
    [en.AiOperationType.weak_point_classification]: "薄弱点分类",
    [en.AiOperationType.weak_point_detection_criteria]: "薄弱点检测标准",
    [en.AiOperationType.weak_point_recheck]: "薄弱点复检",
    [en.AiOperationType.score_challenge_summary]: "分数改判总结",
    [en.AiOperationType.vocab_semantic_drift]: "词汇语义偏移",
  } as LabelMap,
  FollowUpThreadStatusLabel: {
    [en.FollowUpThreadStatus.open]: "追问进行中",
    [en.FollowUpThreadStatus.closed]: "已结束",
  } as LabelMap,
  ScaleTypeLabel: {
    [en.ScaleType.band_1_5]: "Band 1–5",
    [en.ScaleType.score_0_100]: "百分制",
    [en.ScaleType.rubric_level]: "评级档位",
  } as LabelMap,
  CategoryTypeLabel: {
    [en.CategoryType.domain]: "领域",
    [en.CategoryType.scenario]: "场景",
  } as LabelMap,
} as const;

export type EnumLabelName = keyof typeof zh;

const EN_MAPS: Record<EnumLabelName, LabelMap> = {
  WeakPointGenerationStatusLabel: en.WeakPointGenerationStatusLabel,
  FollowUpThreadKindLabel: en.FollowUpThreadKindLabel,
  TaskTypeLabel: en.TaskTypeLabel,
  DifficultyLabel: en.DifficultyLabel,
  SubmissionStatusLabel: en.SubmissionStatusLabel,
  FollowUpVerdictLabel: en.FollowUpVerdictLabel,
  OverrideStatusLabel: en.OverrideStatusLabel,
  WeakPointCatalogStatusLabel: en.WeakPointCatalogStatusLabel,
  WeakPointStatusLabel: en.WeakPointStatusLabel,
  PriorityLabel: en.PriorityLabel,
  MasteryLevelLabel: en.MasteryLevelLabel,
  SubjectCategoryLabel: en.SubjectCategoryLabel,
  TemplateLayerLabel: en.TemplateLayerLabel,
  CheckpointImportanceLabel: en.CheckpointImportanceLabel,
  ErrorSeverityLabel: en.ErrorSeverityLabel,
  AiOperationTypeLabel: en.AiOperationTypeLabel,
  FollowUpThreadStatusLabel: en.FollowUpThreadStatusLabel,
  ScaleTypeLabel: en.ScaleTypeLabel,
  CategoryTypeLabel: en.CategoryTypeLabel,
};

export type EnumLabels = Record<EnumLabelName, LabelMap>;

/** 当前界面语言下的全部枚举标签映射。 */
export function useEnumLabels(): EnumLabels {
  const { locale } = useI18n();
  return useMemo(() => (locale === "zh" ? { ...EN_MAPS, ...zh } : EN_MAPS), [locale]);
}

/** Band 1（最好）→ 5（最差）的文字标签，随语言变。 */
export function useBandLabel(): (band: number) => string {
  const { t } = useI18n();
  return useMemo(
    () =>
      (band: number): string => {
        const n = Math.min(5, Math.max(1, Math.round(band)));
        return t(`band.${n}` as "band.1");
      },
    [t],
  );
}

/** severity → 「是否影响核心意义」标签 + 语气，随语言变。 */
export function useErrorImpactLabel(): (severity: number) => {
  text: string;
  tone: "danger" | "muted";
} {
  const { locale } = useI18n();
  return useMemo(
    () => (severity: number) => {
      if (severity === en.ErrorSeverity.major)
        return {
          text: locale === "zh" ? "影响理解" : "Affects understanding",
          tone: "danger" as const,
        };
      return {
        text: locale === "zh" ? "仅精度损失" : "Accuracy loss only",
        tone: "muted" as const,
      };
    },
    [locale],
  );
}
