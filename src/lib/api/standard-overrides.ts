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

/** 唯一允许的"修改":把一条修正标记为 deprecated(作废)。审计链不做编辑/物理删除。已作废再调返回 409。 */
export async function deprecateStandardOverride(
  id: string,
): Promise<{ id: string; status: number }> {
  return api<{ id: string; status: number }>(`/standard-overrides/${id}/deprecate`, {
    method: "POST",
  });
}
