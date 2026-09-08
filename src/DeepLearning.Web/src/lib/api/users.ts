import { createBrowserApiClient } from "./fetcher";
import type { UserProfile } from "@/lib/types/dtos";
import type { Locale } from "@/lib/i18n/config";

const api = createBrowserApiClient();

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
