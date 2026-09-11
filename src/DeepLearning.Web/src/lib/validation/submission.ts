import { z } from "zod";
import { isValidRange } from "./task-b-ranges";

/**
 * 镜像后端 `CreateSubmissionValidator`（方案 §11、§3.9）：
 * TaskA 的 content 是 JSON 编码的字符串本体；TaskB 的 content 是 JSON 编码的标注数组，
 * 每项含 positionStart/positionEnd(number)、errorCategory(非空字符串)、correctedText(字符串)。
 */

export const taskAContentSchema = z.string().trim().min(1, "v.translationRequired");

export const taskBAnnotationSchema = z
  .object({
    positionStart: z.number().int().nonnegative(),
    positionEnd: z.number().int().nonnegative(),
    errorCategory: z.string().min(1, "v.selectErrorType"),
    correctedText: z.string().min(1, "v.enterCorrectedText"),
  })
  .refine((a) => isValidRange(a.positionStart, a.positionEnd), {
    message: "v.selectionEndGtStart",
    path: ["positionEnd"],
  });

export const taskBContentSchema = z.array(taskBAnnotationSchema).min(1, "v.annotateAtLeastOne");

export type TaskBAnnotationInput = z.infer<typeof taskBAnnotationSchema>;

/** 迁到 task-b-ranges.ts 后在这里重新导出——保持既有 import 路径（question-import.ts、
 * submission.test.ts）不用改。 */
export { findOverlappingAnnotations } from "./task-b-ranges";
