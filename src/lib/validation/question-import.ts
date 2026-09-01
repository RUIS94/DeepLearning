import { z } from "zod";
import { findOverlappingAnnotations } from "./submission";

/**
 * 镜像后端 `ImportUserQuestionValidator`（AGENTS.md 里点名"这是最值得参考的一个校验器"）：
 * TaskB 必须有 flawedTranslationText 且至少一条 seededErrors，
 * 且 positionStart < positionEnd、区间不重叠、都落在 flawedTranslationText.length 以内。
 */

const meaningCheckpointSchema = z.object({
  checkpointText: z.string().min(1, "意义点内容不能为空"),
  checkpointType: z.string().nullable().optional(),
  importance: z.number().int().min(0).max(1),
});

const seededErrorSchema = z.object({
  positionStart: z.number().int().nonnegative(),
  positionEnd: z.number().int().nonnegative(),
  errorTaxonomyId: z.string().min(1, "请选择错误分类"),
  correctReferenceText: z.string().min(1, "请填写正确译法"),
  note: z.string().nullable().optional(),
});

export const importUserQuestionSchema = z
  .object({
    taskType: z.number().int().min(0).max(1),
    difficulty: z.number().int().min(0).max(2),
    title: z.string().trim().min(1, "标题不能为空").max(255),
    // brief 落在后端 jsonb 列（设计文档 §6.2：领域/文本类型/目的/受众）。留空即不填；
    // 一旦填写必须是合法 JSON，否则后端 ImportUserQuestionValidator 会返回 400。
    brief: z
      .string()
      .trim()
      .nullable()
      .optional()
      .refine(
        (v) => {
          if (!v) return true;
          try {
            JSON.parse(v);
            return true;
          } catch {
            return false;
          }
        },
        { message: '简介需为合法 JSON, 例如 {"领域":"公共卫生","文本类型":"通知"}' },
      ),
    sourceText: z.string().trim().min(1, "原文不能为空"),
    wordCount: z.number().int().positive().nullable().optional(),
    isSeedReference: z.boolean().optional(),
    visibility: z.number().int().min(0).max(1).optional(),
    meaningCheckpoints: z.array(meaningCheckpointSchema).optional(),
    taskB: z
      .object({
        flawedTranslationText: z.string().trim(),
        seededErrors: z.array(seededErrorSchema).optional(),
      })
      .nullable()
      .optional(),
  })
  .superRefine((data, ctx) => {
    if (data.taskType !== 1) return;
    const taskB = data.taskB;
    if (!taskB) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "TaskType 为 B 时必须填写含错译文与种子错误",
        path: ["taskB"],
      });
      return;
    }
    const flawedTranslationText = taskB.flawedTranslationText.trim();
    if (!flawedTranslationText) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "含错译文不能为空",
        path: ["taskB", "flawedTranslationText"],
      });
    }
    const seededErrors = taskB.seededErrors ?? [];
    if (seededErrors.length === 0) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "TaskB 至少需要一条种子错误",
        path: ["taskB", "seededErrors"],
      });
    }
    const len = flawedTranslationText.length;
    seededErrors.forEach((e, i) => {
      if (e.positionStart >= e.positionEnd) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: "结束位置必须大于起始位置",
          path: ["taskB", "seededErrors", i, "positionEnd"],
        });
      }
      if (e.positionStart < 0 || e.positionEnd > len) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `区间必须落在含错译文长度（${len}）以内`,
          path: ["taskB", "seededErrors", i, "positionEnd"],
        });
      }
    });
    const overlap = findOverlappingAnnotations(seededErrors);
    if (overlap) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "种子错误的标注区间不能相互重叠",
        path: ["taskB", "seededErrors"],
      });
    }
  });

export type ImportUserQuestionFormInput = z.infer<typeof importUserQuestionSchema>;
