import { createBrowserApiClient } from "./fetcher";
import type {
  CreateSubmissionRequest,
  CreateSubmissionResult,
  GradeSubmissionResult,
  SubmissionDetail,
  SubmissionSummary,
} from "@/lib/types/dtos";

const api = createBrowserApiClient();

/** 当前用户对某题的历史提交(最新在前)。用于题库页"打开做过的记录"。 */
export async function listSubmissions(
  userId: string,
  questionId?: string,
): Promise<SubmissionSummary[]> {
  return api<SubmissionSummary[]>("/submissions", { query: { userId, questionId } });
}

export async function createSubmission(
  req: CreateSubmissionRequest,
): Promise<CreateSubmissionResult> {
  return api<CreateSubmissionResult>("/submissions", { method: "POST", body: req });
}

export async function getSubmissionById(id: string): Promise<SubmissionDetail> {
  return api<SubmissionDetail>(`/submissions/${id}`);
}

/** 真实流程是"评分 → 再 GET 一次 /submissions/{id}"两步——这个函数只返回计数，不含评分结果本身。 */
export async function gradeSubmission(
  id: string,
  examTypeId: string,
): Promise<GradeSubmissionResult> {
  return api<GradeSubmissionResult>(`/submissions/${id}/grade`, {
    method: "POST",
    body: { examTypeId },
  });
}
