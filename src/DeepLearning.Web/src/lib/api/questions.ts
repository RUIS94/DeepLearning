import { createBrowserApiClient } from "./fetcher";
import type {
  CreateQuestionResult,
  GenerateQuestionRequest,
  ImportUserQuestionRequest,
  QuestionDetail,
  QuestionListItem,
  SeedReferenceLink,
} from "@/lib/types/dtos";

const api = createBrowserApiClient();

export async function importUserQuestion(
  req: ImportUserQuestionRequest,
): Promise<CreateQuestionResult> {
  return api<CreateQuestionResult>("/questions", { method: "POST", body: req });
}

export async function generateQuestion(
  req: GenerateQuestionRequest,
): Promise<CreateQuestionResult> {
  return api<CreateQuestionResult>("/questions/generate", { method: "POST", body: req });
}

export async function listQuestions(filter?: {
  taskType?: number | undefined;
  difficulty?: number | undefined;
  categoryId?: string | undefined;
  inBank?: boolean | undefined;
  /** 传了才会带回每题的 myAttemptCount / myLatestSubmissionId。 */
  userId?: string | undefined;
  /** 只列真题种子(供 AI 出题的种子选择列表用)。 */
  isSeedReference?: boolean | undefined;
}): Promise<QuestionListItem[]> {
  return api<QuestionListItem[]>("/questions", {
    query: {
      taskType: filter?.taskType,
      difficulty: filter?.difficulty,
      categoryId: filter?.categoryId,
      inBank: filter?.inBank,
      userId: filter?.userId,
      isSeedReference: filter?.isSeedReference,
    },
  });
}

export async function getQuestionById(id: string): Promise<QuestionDetail> {
  return api<QuestionDetail>(`/questions/${id}`);
}

export async function listSeedReferences(questionId: string): Promise<SeedReferenceLink[]> {
  return api<SeedReferenceLink[]>(`/questions/${questionId}/seed-references`);
}
