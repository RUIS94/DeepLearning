import { createBrowserApiClient } from "./fetcher";
import type {
  ActivateStandardOverrideResult,
  StandardOverride,
  StandardOverrideDetail,
} from "@/lib/types/dtos";

const api = createBrowserApiClient();

export async function listStandardOverrides(status?: number): Promise<StandardOverride[]> {
  return api<StandardOverride[]>("/standard-overrides", { query: { status } });
}

export async function getStandardOverrideById(id: string): Promise<StandardOverrideDetail> {
  return api<StandardOverrideDetail>(`/standard-overrides/${id}`);
}

/** design doc §10.6"或经过一次人工复核"路径——不看累计确认次数直接把 observing 提升为 active。 */
export async function activateStandardOverride(
  id: string,
): Promise<ActivateStandardOverrideResult> {
  return api<ActivateStandardOverrideResult>(`/standard-overrides/${id}/activate`, {
    method: "POST",
  });
}
