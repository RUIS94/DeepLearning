import { createBrowserApiClient } from "./fetcher";
import type { FeatureFlag } from "@/lib/types/dtos";

const api = createBrowserApiClient();

/** 每个已知开关 + 其生效值（有行取行值，无行取代码默认值）。设置页「功能开关」用。 */
export async function listFeatureFlags(): Promise<FeatureFlag[]> {
  return api<FeatureFlag[]>("/feature-flags");
}

export async function setFeatureFlag(
  key: string,
  enabled: boolean,
): Promise<{ key: string; enabled: boolean }> {
  return api<{ key: string; enabled: boolean }>(`/feature-flags/${key}`, {
    method: "PUT",
    body: { enabled },
  });
}
