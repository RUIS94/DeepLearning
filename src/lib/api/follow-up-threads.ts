import { ApiError, createBrowserApiClient } from "./fetcher";
import type {
  AddFollowUpMessageRequest,
  CreateFollowUpThreadRequest,
  FollowUpThreadDetail,
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

export async function closeFollowUpThread(
  threadId: string,
  userId: string,
): Promise<FollowUpThreadDetail> {
  return api<FollowUpThreadDetail>(`/follow-up-threads/${threadId}/close`, {
    method: "POST",
    body: { userId },
  });
}

/** 404（该 submission 还没有线程）当作正常态返回 null，不抛错——调用方用 null 渲染"尚未发起追问"。 */
export async function getFollowUpThreadBySubmission(
  submissionId: string,
): Promise<FollowUpThreadDetail | null> {
  try {
    return await api<FollowUpThreadDetail>(`/follow-up-threads/by-submission/${submissionId}`);
  } catch (err) {
    if (err instanceof ApiError && err.status === 404) {
      return null;
    }
    throw err;
  }
}
