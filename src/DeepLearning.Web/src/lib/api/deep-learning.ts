import { createBrowserApiClient } from "./fetcher";
import type { DeepLearningContent, GenerateDeepLearningContentResponse } from "@/lib/types/dtos";

const api = createBrowserApiClient();

/** 幂等：已生成过会直接返回缓存内容（wasCached=true），不会重复调用 AI（方案 §3.6）。 */
export async function generateDeepLearning(
  questionId: string,
  examTypeId: string,
): Promise<GenerateDeepLearningContentResponse> {
  return api<GenerateDeepLearningContentResponse>(`/questions/${questionId}/deep-learning`, {
    method: "POST",
    body: { examTypeId },
  });
}

export async function getDeepLearningContent(questionId: string): Promise<DeepLearningContent> {
  return api<DeepLearningContent>(`/questions/${questionId}/deep-learning`);
}
