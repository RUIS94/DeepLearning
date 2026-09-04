import { createBrowserApiClient } from "./fetcher";
import type {
  CreateSubmissionRequest,
  CreateSubmissionResult,
  GradeSubmissionAccepted,
  GradingStatusResult,
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

/**
 * 只是把批改任务入队，立刻返回 202——不等结果。
 *
 * 批改是四次 LLM 调用、实测五分钟以上，同步等会稳定超过转发层的 300 秒上限：浏览器拿到 500，
 * 服务端却在后面默默跑完并把结果落库，没人看得到。现在改成入队 + 轮询 GET /submissions/{id}，
 * 直到状态离开 grading。
 */
export async function gradeSubmission(
  id: string,
  examTypeId: string,
): Promise<GradeSubmissionAccepted> {
  return api<GradeSubmissionAccepted>(`/submissions/${id}/grade`, {
    method: "POST",
    body: { examTypeId },
  });
}

/**
 * 长轮询：请求挂在服务端，直到批改结束或 waitSeconds 到点才返回。
 *
 * 定时轮询要在"请求密度"和"结果延迟"之间二选一——30 秒一轮的话，后端早就跑完了，用户还要
 * 盯着转圈半分钟。长轮询两头都要：一分钟才一个请求，而状态一变就在两秒内返回。
 * terminal=false 表示还在跑，客户端原样再问一次即可（绝不重新发起批改）。
 */
export async function watchGradingStatus(
  id: string,
  waitSeconds: number,
): Promise<GradingStatusResult> {
  return api<GradingStatusResult>(`/submissions/${id}/grading-status`, {
    query: { waitSeconds: String(waitSeconds) },
  });
}

/**
 * 重新生成薄弱点。只在生成失败后由用户手动触发——一次生成就是一次 LLM 调用，所以后端不自动重试，
 * 前端也不自动重试。
 */
export async function regenerateWeakPoints(id: string, examTypeId: string): Promise<void> {
  await api<void>(`/submissions/${id}/weak-points/regenerate`, {
    method: "POST",
    body: { examTypeId },
  });
}
