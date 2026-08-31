import { createBrowserApiClient } from "./fetcher";
import type {
  CreateFollowUpQuestionRequest,
  FollowUpQuestionDetail,
  FollowUpQuestionResult,
} from "@/lib/types/dtos";

const api = createBrowserApiClient();

export async function createFollowUp(
  req: CreateFollowUpQuestionRequest,
): Promise<FollowUpQuestionResult> {
  return api<FollowUpQuestionResult>("/follow-ups", { method: "POST", body: req });
}

export async function listFollowUps(submissionId: string): Promise<FollowUpQuestionDetail[]> {
  return api<FollowUpQuestionDetail[]>("/follow-ups", { query: { submissionId } });
}

export async function getFollowUpQuestionById(id: string): Promise<FollowUpQuestionDetail> {
  return api<FollowUpQuestionDetail>(`/follow-ups/${id}`);
}
