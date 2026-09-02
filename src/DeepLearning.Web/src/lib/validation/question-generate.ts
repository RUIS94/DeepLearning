import { z } from "zod";

/**
 * 镜像后端 `GenerateQuestionCommand` 校验规则（方案 §11）：
 * seedQuestionIds 最多 5 个、不能重复、不能含空字符串。
 * 注：生产环境 id 是后端 GUID，这里只做非空校验，真实格式在接后端时可以换成 z.string().uuid()。
 */
export const generateQuestionFormSchema = z.object({
  examTypeId: z.string().min(1),
  taskType: z.number().int().min(0).max(1),
  difficulty: z.number().int().min(0).max(2).nullable().optional(),
  categoryId: z.string().min(1).nullable().optional(),
  seedQuestionIds: z
    .array(z.string().min(1, "真题种子 id 不能为空"))
    .max(5, "真题种子最多选择 5 道")
    .refine((ids) => new Set(ids).size === ids.length, {
      message: "真题种子不能重复选择",
    })
    .nullable()
    .optional(),
  targetWeakPoints: z.boolean().optional(),
});

export type GenerateQuestionFormInput = z.infer<typeof generateQuestionFormSchema>;
