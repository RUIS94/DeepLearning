import { createBrowserApiClient } from "./fetcher";
import type { UserProfile } from "@/lib/types/dtos";

const api = createBrowserApiClient();

export async function getUserById(id: string): Promise<UserProfile> {
  return api<UserProfile>(`/users/${id}`);
}
