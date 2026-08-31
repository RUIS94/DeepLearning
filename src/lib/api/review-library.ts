import { createBrowserApiClient } from "./fetcher";
import type { ReviewPatternItem, ReviewVocabItem } from "@/lib/types/dtos";

const api = createBrowserApiClient();

export async function listReviewPatterns(userId: string): Promise<ReviewPatternItem[]> {
  return api<ReviewPatternItem[]>("/review-library/patterns", { query: { userId } });
}

export async function listReviewVocab(userId: string): Promise<ReviewVocabItem[]> {
  return api<ReviewVocabItem[]>("/review-library/vocab", { query: { userId } });
}

export async function reviewPattern(userId: string, id: string, masteryLevel: number) {
  return api(`/review-library/patterns/${id}/review`, {
    method: "POST",
    body: { userId, masteryLevel },
  });
}

export async function reviewVocabItem(userId: string, id: string, masteryLevel: number) {
  return api(`/review-library/vocab/${id}/review`, {
    method: "POST",
    body: { userId, masteryLevel },
  });
}
