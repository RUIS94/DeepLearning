"use client";

import type { ReactNode } from "react";
import { PageShell } from "@/components/shell/page-shell";

/**
 * 兼容层:侧栏/导航由 (app)/layout.tsx 的 AppSidebar 统一提供;标题区固定、只有内容区滚动由
 * PageShell 负责。现有页面 `<AppShell title=... description=... actions=...>{children}</AppShell>`
 * 无需改动。新页面可直接用 <PageShell />。
 */
export function AppShell({
  title,
  description,
  actions,
  back,
  backHref,
  children,
}: {
  title: string;
  description?: string | undefined;
  actions?: ReactNode;
  back?: boolean;
  backHref?: string;
  children: ReactNode;
}) {
  return (
    <PageShell
      title={title}
      description={description}
      actions={actions}
      back={back}
      backHref={backHref}
    >
      {children}
    </PageShell>
  );
}
