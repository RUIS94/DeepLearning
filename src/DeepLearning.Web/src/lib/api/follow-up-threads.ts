import { createBrowserApiClient } from "./fetcher";
import type {
  AddFollowUpMessageRequest,
  CreateFollowUpThreadRequest,
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

export async function closeFollowUpThread(
  threadId: string,
  userId: string,
): Promise<FollowUpThreadDetail> {
  return api<FollowUpThreadDetail>(`/follow-up-threads/${threadId}/close`, {
    method: "POST",
    body: { userId },
  });
}

/** 该 submission 的所有追问线程，最新在前；没有则空数组。 */
export async function listFollowUpThreads(submissionId: string): Promise<FollowUpThreadSummary[]> {
  return api<FollowUpThreadSummary[]>("/follow-up-threads", { query: { submissionId } });
}

export async function getFollowUpThread(threadId: string): Promise<FollowUpThreadDetail> {
  return api<FollowUpThreadDetail>(`/follow-up-threads/${threadId}`);
}
