import { createBrowserApiClient } from "./fetcher";
import type { FollowUpQuestionDetail } from "@/lib/types/dtos";

const api = createBrowserApiClient();

// POST /follow-ups (single-shot CreateFollowUpQuestionCommand) was retired in favor of the
// multi-round thread model — see lib/api/follow-up-threads.ts. These two GET endpoints stay
// backend-side, read-only, for historical audit of rows created before that change.

export async function listFollowUps(submissionId: string): Promise<FollowUpQuestionDetail[]> {
  return api<FollowUpQuestionDetail[]>("/follow-ups", { query: { submissionId } });
}

export async function getFollowUpQuestionById(id: string): Promise<FollowUpQuestionDetail> {
  return api<FollowUpQuestionDetail>(`/follow-ups/${id}`);
}
