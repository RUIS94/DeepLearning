import { z } from "zod";

/**
 * 镜像后端 `CreateSubmissionValidator`（方案 §11、§3.9）：
 * TaskA 的 content 是 JSON 编码的字符串本体；TaskB 的 content 是 JSON 编码的标注数组，
 * 每项含 positionStart/positionEnd(number)、errorCategory(非空字符串)、correctedText(字符串)。
 */

export const taskAContentSchema = z.string().trim().min(1, "译文不能为空");

export const taskBAnnotationSchema = z
  .object({
    positionStart: z.number().int().nonnegative(),
    positionEnd: z.number().int().nonnegative(),
    errorCategory: z.string().min(1, "请选择错误类型"),
    correctedText: z.string().min(1, "请填写修正后的文本"),
  })
  .refine((a) => a.positionEnd > a.positionStart, {
    message: "选区结束位置必须大于起始位置",
    path: ["positionEnd"],
  });

export const taskBContentSchema = z.array(taskBAnnotationSchema).min(1, "至少标注一处错误才能提交");

export type TaskBAnnotationInput = z.infer<typeof taskBAnnotationSchema>;

/** 校验一组标注互不重叠（后端同一规则用于 TaskB 手工导入，这里对提交侧一并做前端提前拦截）。 */
export function findOverlappingAnnotations(
  annotations: { positionStart: number; positionEnd: number }[],
) {
  const sorted = [...annotations].sort((a, b) => a.positionStart - b.positionStart);
  for (let i = 1; i < sorted.length; i++) {
    if (sorted[i]!.positionStart < sorted[i - 1]!.positionEnd) {
      return [sorted[i - 1], sorted[i]] as const;
    }
  }
  return null;
}
