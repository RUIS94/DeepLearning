import { createBrowserApiClient } from "./fetcher";
import type { UserProfile } from "@/lib/types/dtos";
import type { Locale } from "@/lib/i18n/config";

const api = createBrowserApiClient();

/** 对应后端 GET /users/me——始终是调用方自己的 profile，替代下面按 id 读取（仅 self 或 admin 可用）。 */
export async function getCurrentUser(): Promise<UserProfile> {
  return api<UserProfile>("/users/me");
}

/** self 或 admin 才能读；读别人会 403。管理员用户列表见 lib/api/admin-users.ts。 */
export async function getUserById(id: string): Promise<UserProfile> {
  return api<UserProfile>(`/users/${id}`);
}

/** 对应后端 PUT /users/{id}/language-preference。切换失败不影响本地已生效的界面语言。 */
export async function updateUserLanguagePreference(
  id: string,
  languagePreference: Locale,
): Promise<{ id: string; languagePreference: Locale }> {
  return api(`/users/${id}/language-preference`, {
    method: "PUT",
    body: { languagePreference },
  });
}
