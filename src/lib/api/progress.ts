import { createBrowserApiClient } from "./fetcher";
import type { ProgressSnapshot } from "@/lib/types/dtos";

const api = createBrowserApiClient();

export async function listProgress(
  userId: string,
  difficultyTier?: string,
): Promise<ProgressSnapshot[]> {
  return api<ProgressSnapshot[]>("/progress", { query: { userId, difficultyTier } });
}
