import { createBrowserApiClient } from "./fetcher";
import type { WeakPoint } from "@/lib/types/dtos";

const api = createBrowserApiClient();

export async function listWeakPoints(userId: string, status?: number): Promise<WeakPoint[]> {
  return api<WeakPoint[]>("/weak-points", { query: { userId, status } });
}
