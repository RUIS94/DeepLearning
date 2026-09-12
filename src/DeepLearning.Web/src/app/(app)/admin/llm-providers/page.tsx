import type { Metadata } from "next";
import { LlmProvidersPage } from "./llm-providers-page";

export const metadata: Metadata = { title: "AI Providers" };

/**
 * Admin-only (A1, ref/管理员与用户权限隔离_策划书.md) — moved out of Settings, which every
 * logged-in user could reach; the backend's LlmProviderSettingsController is AdminOnly end to
 * end now. Role guard lives in app/(app)/admin/layout.tsx.
 */
export default function Page() {
  return <LlmProvidersPage />;
}
