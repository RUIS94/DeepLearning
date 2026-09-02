import { createBrowserApiClient } from "./fetcher";
import type {
  AssessmentDimension,
  CreateAssessmentDimensionRequest,
  CreateErrorTaxonomyRequest,
  CreateExamTypeRequest,
  CreateExamTypeResult,
  CreatePromptTemplateRequest,
  CreateQuestionBankCategoryRequest,
  ErrorTaxonomy,
  ExamType,
  ExamTypeDetail,
  PromptTemplate,
  QuestionBankCategory,
} from "@/lib/types/dtos";

const api = createBrowserApiClient();

export async function listExamTypes(): Promise<ExamType[]> {
  return api<ExamType[]>("/exam-types");
}

export async function getExamTypeById(id: string): Promise<ExamTypeDetail> {
  return api<ExamTypeDetail>(`/exam-types/${id}`);
}

export async function createExamType(req: CreateExamTypeRequest): Promise<CreateExamTypeResult> {
  return api<CreateExamTypeResult>("/exam-types", { method: "POST", body: req });
}

export async function listAssessmentDimensions(examTypeId: string): Promise<AssessmentDimension[]> {
  return api<AssessmentDimension[]>(`/exam-types/${examTypeId}/assessment-dimensions`);
}

/** examTypeId 是路径参数，不在 body 里（见 dtos.ts CreateAssessmentDimensionRequest 的注释）。 */
export async function createAssessmentDimension(
  examTypeId: string,
  req: CreateAssessmentDimensionRequest,
): Promise<AssessmentDimension> {
  return api<AssessmentDimension>(`/exam-types/${examTypeId}/assessment-dimensions`, {
    method: "POST",
    body: req,
  });
}

export async function listErrorTaxonomiesByExamType(examTypeId: string): Promise<ErrorTaxonomy[]> {
  return api<ErrorTaxonomy[]>(`/exam-types/${examTypeId}/error-taxonomies`);
}

/** examTypeId 是路径参数，不在 body 里。 */
export async function createErrorTaxonomy(
  examTypeId: string,
  req: CreateErrorTaxonomyRequest,
): Promise<ErrorTaxonomy> {
  return api<ErrorTaxonomy>(`/exam-types/${examTypeId}/error-taxonomies`, {
    method: "POST",
    body: req,
  });
}

export async function listPromptTemplates(filter?: {
  examTypeId?: string | undefined;
  subjectCategory?: number | undefined;
  templateType?: number | undefined;
  /** 省略 = 只返回启用中的(后端默认);传 false 可看停用的。管理页传 undefined 拿全部则需分别取。 */
  isActive?: boolean | undefined;
}): Promise<PromptTemplate[]> {
  return api<PromptTemplate[]>("/prompt-templates", {
    query: {
      examTypeId: filter?.examTypeId,
      subjectCategory: filter?.subjectCategory,
      templateType: filter?.templateType,
      isActive: filter?.isActive,
    },
  });
}

export async function createPromptTemplate(
  req: CreatePromptTemplateRequest,
): Promise<PromptTemplate> {
  return api<PromptTemplate>("/prompt-templates", { method: "POST", body: req });
}

export async function updatePromptTemplate(
  id: string,
  req: { templateContent: string; version: number; isActive: boolean },
): Promise<PromptTemplate> {
  return api<PromptTemplate>(`/prompt-templates/${id}`, { method: "PUT", body: req });
}

export async function deletePromptTemplate(id: string): Promise<void> {
  await api<void>(`/prompt-templates/${id}`, { method: "DELETE" });
}

export async function listCategories(): Promise<QuestionBankCategory[]> {
  return api<QuestionBankCategory[]>("/question-bank-categories");
}

export async function createQuestionBankCategory(
  req: CreateQuestionBankCategoryRequest,
): Promise<QuestionBankCategory> {
  return api<QuestionBankCategory>("/question-bank-categories", { method: "POST", body: req });
}

export async function updateQuestionBankCategory(
  id: string,
  req: { name: string; parentId?: string | null; description?: string | null },
): Promise<QuestionBankCategory> {
  return api<QuestionBankCategory>(`/question-bank-categories/${id}`, { method: "PUT", body: req });
}

/** 后端在有子分类或被题目引用时返回 409。 */
export async function deleteQuestionBankCategory(id: string): Promise<void> {
  await api<void>(`/question-bank-categories/${id}`, { method: "DELETE" });
}

/** 对应 QuestionBankCategoriesController 的 "给题目打标签" action。 */
export async function tagQuestionWithCategory(categoryId: string, questionId: string) {
  return api(`/question-bank-categories/${categoryId}/questions/${questionId}`, {
    method: "POST",
  });
}
