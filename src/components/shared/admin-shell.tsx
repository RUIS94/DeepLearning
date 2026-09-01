"use client";

import type { ReactNode } from "react";
import { PageShell } from "@/components/shell/page-shell";

/**
 * 兼容层:不再区分学生端/管理端,也不再有独立的 admin 顶栏与「无角色鉴权」提示条。这些页面现在
 * 和其它页面一样挂在 (app)/layout.tsx 的侧栏下,标题区固定、只有内容区滚动由 PageShell 负责。
 * 现有页面 `<AdminShell title=... description=... actions=...>{children}</AdminShell>` 无需改动。
 */
export function AdminShell({
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
