import { createBrowserApiClient } from "./fetcher";
import type { AdminUserListResult } from "@/lib/types/dtos";

const api = createBrowserApiClient();

/** 对应后端 GET /admin/users（admin-only）。 */
export async function listUsers(page = 1, pageSize = 200): Promise<AdminUserListResult> {
  return api<AdminUserListResult>(`/admin/users?page=${page}&pageSize=${pageSize}`);
}

export interface UserFeatureOverrideItem {
  featureKey: string;
  enabled: boolean;
}

/** 对应后端 GET /admin/users/{id}/features（admin-only）——只返回这个用户已存在的 override 行。 */
export async function listUserFeatureOverrides(id: string): Promise<UserFeatureOverrideItem[]> {
  return api<UserFeatureOverrideItem[]>(`/admin/users/${id}/features`);
}

/** 对应后端 PUT /admin/users/{id}/role（admin-only）。 */
export async function updateUserRole(
  id: string,
  role: number,
): Promise<{ id: string; role: number }> {
  return api(`/admin/users/${id}/role`, { method: "PUT", body: { role } });
}

/**
 * 对应后端 PUT /admin/users/{id}/features/{key}（admin-only）。
 * enabled=null 清除覆盖，回退到全局 feature_flags 的值。
 */
export async function setUserFeatureOverride(
  id: string,
  key: string,
  enabled: boolean | null,
): Promise<{ userId: string; featureKey: string; enabled: boolean | null }> {
  return api(`/admin/users/${id}/features/${key}`, { method: "PUT", body: { enabled } });
}
