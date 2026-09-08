import { z } from "zod";
import { findOverlappingAnnotations } from "./submission";

/**
 * 镜像后端 `ImportUserQuestionValidator`（AGENTS.md 里点名"这是最值得参考的一个校验器"）：
 * TaskB 必须有 flawedTranslationText 且至少一条 seededErrors，
 * 且 positionStart < positionEnd、区间不重叠、都落在 flawedTranslationText.length 以内。
 */

const meaningCheckpointSchema = z.object({
  checkpointText: z.string().min(1, "Meaning-point text is required"),
  checkpointType: z.string().nullable().optional(),
  importance: z.number().int().min(0).max(1),
});

const seededErrorSchema = z.object({
  positionStart: z.number().int().nonnegative(),
  positionEnd: z.number().int().nonnegative(),
  errorTaxonomyId: z.string().min(1, "Select an error category"),
  correctReferenceText: z.string().min(1, "Enter the correct translation"),
  note: z.string().nullable().optional(),
});

export const importUserQuestionSchema = z
  .object({
    taskType: z.number().int().min(0).max(1),
    difficulty: z.number().int().min(0).max(2),
    title: z.string().trim().min(1, "Title is required").max(255),
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
    sourceText: z.string().trim().min(1, "Source text is required"),
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
        message: "TaskB requires a flawed translation and seeded errors",
        path: ["taskB"],
      });
      return;
    }
    const flawedTranslationText = taskB.flawedTranslationText.trim();
    if (!flawedTranslationText) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "Flawed translation is required",
        path: ["taskB", "flawedTranslationText"],
      });
    }
    const seededErrors = taskB.seededErrors ?? [];
    if (seededErrors.length === 0) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "TaskB needs at least one seeded error",
        path: ["taskB", "seededErrors"],
      });
    }
    const len = flawedTranslationText.length;
    seededErrors.forEach((e, i) => {
      if (e.positionStart >= e.positionEnd) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: "End position must be greater than start position",
          path: ["taskB", "seededErrors", i, "positionEnd"],
        });
      }
      if (e.positionStart < 0 || e.positionEnd > len) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `The range must fall within the flawed translation length (${len})`,
          path: ["taskB", "seededErrors", i, "positionEnd"],
        });
      }
    });
    const overlap = findOverlappingAnnotations(seededErrors);
    if (overlap) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        message: "Seeded error ranges cannot overlap",
        path: ["taskB", "seededErrors"],
      });
    }
  });

export type ImportUserQuestionFormInput = z.infer<typeof importUserQuestionSchema>;
