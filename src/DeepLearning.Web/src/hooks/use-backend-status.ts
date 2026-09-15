"use client";

import { useQuery } from "@tanstack/react-query";
import { qk } from "@/lib/query-keys";

/**
 * 后端（用户家里 Docker 常驻跑的 .NET API，经 Cloudflare Tunnel 接到公网）是否在线——轮询
 * `/api/health`（route.ts 直接探测 `${BACKEND_API_BASE_URL}/health`，见该文件注释）。用于
 * components/shared/backend-status-banner.tsx 的离线提示条。
 *
 * 20s 轮询：够快能让用户几乎实时看到恢复，又不至于给 Vercel 函数量/家里带宽添太多负担。
 * 初始 `data` 是 undefined（还没探测过一次），isOffline 在那期间保持 false——不想开屏就先闪一条
 * "离线"横幅。
 */
export function useBackendStatus(): { isOffline: boolean } {
  const { data } = useQuery({
    queryKey: qk.backendStatus(),
    queryFn: async (): Promise<boolean> => {
      const res = await fetch("/api/health", { cache: "no-store" });
      const body = (await res.json()) as { online: boolean };
      return body.online;
    },
    refetchInterval: 20_000,
    refetchOnWindowFocus: true,
    retry: false,
    staleTime: 0,
  });

  return { isOffline: data === false };
}
