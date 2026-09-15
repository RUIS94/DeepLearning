"use client";

import { useBackendStatus } from "@/hooks/use-backend-status";
import { useT } from "@/lib/i18n";

/**
 * 后端离线时的全局提示条——挂在 app/providers.tsx 顶层，覆盖所有页面（包括登录页：登录本身
 * 走 Supabase 直连，和这个 .NET 后端是否在线无关，但登录之后几乎所有功能都要靠它）。
 */
export function BackendStatusBanner() {
  const { isOffline } = useBackendStatus();
  const t = useT();

  if (!isOffline) return null;

  return (
    <div
      role="status"
      className="sticky top-0 z-50 w-full bg-destructive px-4 py-2 text-center text-sm text-destructive-foreground"
    >
      {t("backend.offline")}
    </div>
  );
}
