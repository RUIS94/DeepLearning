import { z } from "zod";

/** admin 六个资源的“新建”表单 schema。所有资源都是 Create+GetById+List，没有 Update/Delete（方案 §3.7）。 */

export const examTypeFormSchema = z.object({
  code: z
    .string()
    .trim()
    .min(1, "code 不能为空")
    .regex(/^[a-z0-9_]+$/, "只能包含小写字母、数字与下划线"),
  name: z.string().trim().min(1, "名称不能为空").max(100),
  subjectCategory: z.number().int().min(0).max(4),
  sourceLanguage: z.string().trim().max(20).nullable().optional(),
  targetLanguage: z.string().trim().max(20).nullable().optional(),
  gradeLevel: z.string().trim().max(50).nullable().optional(),
  description: z.string().trim().nullable().optional(),
});
export type ExamTypeFormInput = z.infer<typeof examTypeFormSchema>;

export const assessmentDimensionFormSchema = z.object({
  dimensionKey: z
    .string()
    .trim()
    .min(1, "dimensionKey 不能为空")
    .regex(/^[a-z0-9_]+$/, "只能包含小写字母、数字与下划线"),
  dimensionName: z.string().trim().min(1, "名称不能为空").max(100),
  scaleType: z.number().int().min(0).max(2),
  passThreshold: z.string().trim().max(20).nullable().optional(),
  applicableTaskType: z.number().int().min(0).max(1).nullable().optional(),
  levelDescriptions: z.string().trim().min(1, "各 Band 描述不能为空"),
  rubricVersion: z.string().trim().min(1, "rubric 版本号不能为空").max(20),
  effectiveFrom: z.string().trim().min(1, "生效日期不能为空"),
  sourceReference: z.string().trim().nullable().optional(),
});
export type AssessmentDimensionFormInput = z.infer<typeof assessmentDimensionFormSchema>;

export const errorTaxonomyFormSchema = z.object({
  categoryKey: z
    .string()
    .trim()
    .min(1, "categoryKey 不能为空")
    .regex(/^[a-z0-9_]+$/, "只能包含小写字母、数字与下划线"),
  categoryName: z.string().trim().min(1, "名称不能为空").max(100),
  description: z.string().trim().nullable().optional(),
  exampleCases: z.string().trim().nullable().optional(),
});
export type ErrorTaxonomyFormInput = z.infer<typeof errorTaxonomyFormSchema>;

/** subjectCategory 用 -1 表示表单里的“不关联”哨兵值，提交前需转换为 null 再传给后端形状。 */
export const promptTemplateFormSchema = z
  .object({
    examTypeId: z.string().nullable().optional(),
    subjectCategory: z.number().int().min(-1).max(4).nullable().optional(),
    // 0-5：AiOperationType 的 6 个值（question_gen/grading/followup/standard_revision/
    // deep_learning/progress_trend），不是只有前 4 个。
    templateType: z.number().int().min(0).max(5),
    layer: z.number().int().min(0).max(1),
    templateContent: z.string().trim().min(1, "模板正文不能为空"),
    // 后端 CreatePromptTemplateCommand 要求显式传版本号，没有自动递增逻辑。
    version: z.number().int().min(1, "版本号至少为 1"),
  })
  .refine(
    (v) => {
      const hasExamType = Boolean(v.examTypeId);
      const hasSubject =
        v.subjectCategory !== null && v.subjectCategory !== undefined && v.subjectCategory !== -1;
      return hasExamType !== hasSubject; // XOR：二选一，见设计文档 §6.24
    },
    {
      message: "考试类型与学科类别二选一，不能同时为空或同时填写",
      path: ["examTypeId"],
    },
  );
export type PromptTemplateFormInput = z.infer<typeof promptTemplateFormSchema>;

export const questionBankCategoryFormSchema = z.object({
  categoryType: z.number().int().min(0).max(1),
  name: z.string().trim().min(1, "名称不能为空").max(100),
  parentId: z.string().nullable().optional(),
  description: z.string().trim().nullable().optional(),
});
export type QuestionBankCategoryFormInput = z.infer<typeof questionBankCategoryFormSchema>;
