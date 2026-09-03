import { createBrowserApiClient } from "./fetcher";
import type { WeakPoint } from "@/lib/types/dtos";

const api = createBrowserApiClient();

export async function listWeakPoints(userId: string, status?: number): Promise<WeakPoint[]> {
  return api<WeakPoint[]>("/weak-points", { query: { userId, status } });
}

export async function reclassifyWeakPoint(
  weakPointId: string,
  catalogId: string,
): Promise<{ weakPointId: string; catalogId: string; mergedIntoExisting: boolean }> {
  return api<{ weakPointId: string; catalogId: string; mergedIntoExisting: boolean }>(
    `/weak-points/${weakPointId}/reclassify`,
    { method: "POST", body: { catalogId } },
  );
}
