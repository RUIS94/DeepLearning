import { z } from "zod";

/** admin 六个资源的“新建”表单 schema。所有资源都是 Create+GetById+List，没有 Update/Delete（方案 §3.7）。 */

export const examTypeFormSchema = z.object({
  code: z
    .string()
    .trim()
    .min(1, "v.codeRequired")
    .regex(/^[a-z0-9_]+$/, "v.snakeCaseOnly"),
  name: z.string().trim().min(1, "v.nameRequired").max(100),
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
    .min(1, "v.dimensionKeyRequired")
    .regex(/^[a-z0-9_]+$/, "v.snakeCaseOnly"),
  dimensionName: z.string().trim().min(1, "v.nameRequired").max(100),
  scaleType: z.number().int().min(0).max(2),
  passThreshold: z.string().trim().max(20).nullable().optional(),
  applicableTaskType: z.number().int().min(0).max(1).nullable().optional(),
  levelDescriptions: z.string().trim().min(1, "v.bandDescriptionsRequired"),
  rubricVersion: z.string().trim().min(1, "v.rubricVersionRequired").max(20),
  effectiveFrom: z.string().trim().min(1, "v.effectiveDateRequired"),
  sourceReference: z.string().trim().nullable().optional(),
});
export type AssessmentDimensionFormInput = z.infer<typeof assessmentDimensionFormSchema>;

export const errorTaxonomyFormSchema = z.object({
  categoryKey: z
    .string()
    .trim()
    .min(1, "v.categoryKeyRequired")
    .regex(/^[a-z0-9_]+$/, "v.snakeCaseOnly"),
  categoryName: z.string().trim().min(1, "v.nameRequired").max(100),
  description: z.string().trim().nullable().optional(),
  exampleCases: z.string().trim().nullable().optional(),
});
export type ErrorTaxonomyFormInput = z.infer<typeof errorTaxonomyFormSchema>;

export const weakPointCatalogFormSchema = z.object({
  categoryId: z.string().trim().min(1, "v.selectTopLevelCategory"),
  code: z
    .string()
    .trim()
    .min(1, "v.codeRequired")
    .max(60)
    .regex(/^[a-z0-9_]+$/, "v.snakeCaseOnly"),
  name: z.string().trim().min(1, "v.nameRequired").max(100),
  description: z.string().trim().min(1, "v.descriptionRequired"),
  defaultDimensionKey: z.string().trim().max(50).nullable().optional(),
  defaultErrorCategory: z.string().trim().max(50).nullable().optional(),
  status: z.string().trim().min(1),
});
export type WeakPointCatalogFormInput = z.infer<typeof weakPointCatalogFormSchema>;

/** subjectCategory 用 -1 表示表单里的“不关联”哨兵值，提交前需转换为 null 再传给后端形状。 */
export const promptTemplateFormSchema = z
  .object({
    examTypeId: z.string().nullable().optional(),
    subjectCategory: z.number().int().min(-1).max(4).nullable().optional(),
    // 0-9：AiOperationType 的 10 个值（question_gen/grading/followup/standard_revision/
    // deep_learning/progress_trend/followup_summary/weak_point_classification/
    // weak_point_detection_criteria/weak_point_recheck）。新增枚举值时这里要同步放宽上界，
    // 否则编辑该类模板时 zodResolver 会静默拦下提交（见 enums.ts AiOperationType）。
    templateType: z.number().int().min(0).max(9),
    layer: z.number().int().min(0).max(1),
    templateContent: z.string().trim().min(1, "v.templateContentRequired"),
    // 后端 CreatePromptTemplateCommand 要求显式传版本号，没有自动递增逻辑。
    version: z.number().int().min(1, "v.versionMin1"),
    // 仅编辑时有意义（PUT /prompt-templates/{id}）；新建时后端固定 IsActive=true。
    isActive: z.boolean().optional(),
  })
  .refine(
    (v) => {
      const hasExamType = Boolean(v.examTypeId);
      const hasSubject =
        v.subjectCategory !== null && v.subjectCategory !== undefined && v.subjectCategory !== -1;
      return hasExamType !== hasSubject; // XOR：二选一，见设计文档 §6.24
    },
    {
      message: "v.examTypeSubjectXor",
      path: ["examTypeId"],
    },
  );
export type PromptTemplateFormInput = z.infer<typeof promptTemplateFormSchema>;

export const questionBankCategoryFormSchema = z.object({
  categoryType: z.number().int().min(0).max(1),
  name: z.string().trim().min(1, "v.nameRequired").max(100),
  parentId: z.string().nullable().optional(),
  description: z.string().trim().nullable().optional(),
  /** "" = 全局分类；否则是某个考试类型的 id。 */
  examTypeId: z.string().optional(),
});
export type QuestionBankCategoryFormInput = z.infer<typeof questionBankCategoryFormSchema>;
