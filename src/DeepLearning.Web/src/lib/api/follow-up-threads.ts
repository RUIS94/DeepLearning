import { createBrowserApiClient } from "./fetcher";
import type {
  AddFollowUpMessageRequest,
  CreateFollowUpThreadRequest,
  FollowUpClosePreview,
  FollowUpCloseInput,
  FollowUpThreadDetail,
  FollowUpThreadSummary,
} from "@/lib/types/dtos";

const api = createBrowserApiClient();

export async function createFollowUpThread(
  req: CreateFollowUpThreadRequest,
): Promise<FollowUpThreadDetail> {
  return api<FollowUpThreadDetail>("/follow-up-threads", { method: "POST", body: req });
}

export async function addFollowUpMessage(
  threadId: string,
  req: AddFollowUpMessageRequest,
): Promise<FollowUpThreadDetail> {
  return api<FollowUpThreadDetail>(`/follow-up-threads/${threadId}/messages`, {
    method: "POST",
    body: req,
  });
}

/** 生成结算草稿（会发一次 AI 调用），线程保持 open、不落库。 */
export async function previewFollowUpClose(
  threadId: string,
  userId: string,
): Promise<FollowUpClosePreview> {
  return api<FollowUpClosePreview>(`/follow-up-threads/${threadId}/close/preview`, {
    method: "POST",
    body: { userId },
  });
}

/** input 为用户确认/编辑后的结算内容；不传则后端自己跑 AI 结算（旧行为，knowledge 线程恒不跑）。 */
export async function closeFollowUpThread(
  threadId: string,
  userId: string,
  input?: FollowUpCloseInput,
): Promise<FollowUpThreadDetail> {
  return api<FollowUpThreadDetail>(`/follow-up-threads/${threadId}/close`, {
    method: "POST",
    body: input ? { userId, input } : { userId },
  });
}

/** 该 submission 的所有追问线程，最新在前；没有则空数组。 */
export async function listFollowUpThreads(submissionId: string): Promise<FollowUpThreadSummary[]> {
  return api<FollowUpThreadSummary[]>("/follow-up-threads", { query: { submissionId } });
}

export async function getFollowUpThread(threadId: string): Promise<FollowUpThreadDetail> {
  return api<FollowUpThreadDetail>(`/follow-up-threads/${threadId}`);
}
