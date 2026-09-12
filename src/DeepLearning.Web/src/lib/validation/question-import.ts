import { z } from "zod";
import { findOverlappingAnnotations, isValidRange, isWithinBounds } from "./task-b-ranges";

/**
 * 镜像后端 `ImportUserQuestionValidator`（AGENTS.md 里点名"这是最值得参考的一个校验器"）：
 * TaskB 必须有 flawedTranslationText 且至少一条 seededErrors，
 * 且 positionStart < positionEnd、区间不重叠、都落在 flawedTranslationText.length 以内。
 */

const meaningCheckpointSchema = z.object({
  checkpointText: z.string().min(1, "v.meaningPointRequired"),
  checkpointType: z.string().nullable().optional(),
  importance: z.number().int().min(0).max(1),
});

const seededErrorSchema = z.object({
  positionStart: z.number().int().nonnegative(),
  positionEnd: z.number().int().nonnegative(),
  errorTaxonomyId: z.string().min(1, "v.selectErrorCategory"),
  correctReferenceText: z.string().min(1, "v.enterCorrectTranslation"),
  note: z.string().nullable().optional(),
});

export const importUserQuestionSchema = z
  .object({
    taskType: z.number().int().min(0).max(1),
    difficulty: z.number().int().min(0).max(2),
    title: z.string().trim().min(1, "v.titleRequired").max(255),
    // brief 落在后端 jsonb 列（设计文档 §6.2：领域/文本类型/目的/受众）。四个子字段都可留空；
    // 面板在提交前把非空项拼成 JSON 字符串塞进后端 brief 列，全空则传 null。
    brief: z
      .object({
        domain: z.string().trim().max(255).optional(),
        textType: z.string().trim().max(255).optional(),
        purpose: z.string().trim().max(255).optional(),
        audience: z.string().trim().max(255).optional(),
      })
      .optional(),
    sourceText: z.string().trim().min(1, "v.sourceTextRequired"),
    wordCount: z.number().int().positive().nullable().optional(),
    // Admin-only field (D2'/A2', ref/管理员与用户权限隔离_策划书.md) — the regular user-facing
    // import panel never sets this; only the admin seed-import page does. visibility is NOT part
    // of this schema at all anymore: the backend derives it from isSeedReference and never accepts
    // a caller-supplied value.
    isSeedReference: z.boolean().optional(),
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
        message: "v.taskBRequires",
        path: ["taskB"],
      });
      return;
    }
    const flawedTranslationText = taskB.flawedTranslationText.trim();
    if (!flawedTranslationText) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "v.flawedTranslationRequired",
        path: ["taskB", "flawedTranslationText"],
      });
    }
    const seededErrors = taskB.seededErrors ?? [];
    if (seededErrors.length === 0) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "v.taskBNeedsOneError",
        path: ["taskB", "seededErrors"],
      });
    }
    const len = flawedTranslationText.length;
    seededErrors.forEach((e, i) => {
      if (!isValidRange(e.positionStart, e.positionEnd)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: "v.endGtStartPosition",
          path: ["taskB", "seededErrors", i, "positionEnd"],
        });
      }
      if (!isWithinBounds(e.positionStart, e.positionEnd, len)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `v.rangeWithinLength::${len}`,
          path: ["taskB", "seededErrors", i, "positionEnd"],
        });
      }
    });
    const overlap = findOverlappingAnnotations(seededErrors);
    if (overlap) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "v.seededErrorsNoOverlap",
        path: ["taskB", "seededErrors"],
      });
    }
  });

export type ImportUserQuestionFormInput = z.infer<typeof importUserQuestionSchema>;
