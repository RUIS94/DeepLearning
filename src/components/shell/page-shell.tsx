"use client";

import type { ReactNode } from "react";
import { cn } from "@/lib/utils";
import { PageHeader } from "@/components/shell/page-header";

/**
 * 每个页面的统一容器:标题区固定不滚动,滚动只发生在内容区。
 * (app)/layout.tsx 已把外层高度锁成视口,这里靠 flex 把标题区 shrink-0、内容区 flex-1 + overflow-y-auto。
 *
 * 现有页面通过兼容层 AppShell / AdminShell 间接用到它;新页面可直接:
 *   <PageShell title="标题" description="…" back backHref="/practice" actions={<Button/>}>
 *     {content}
 *   </PageShell>
 */
export function PageShell({
  title,
  description,
  actions,
  back,
  backHref,
  bodyClassName,
  children,
}: {
  title: ReactNode;
  description?: ReactNode | undefined;
  actions?: ReactNode | undefined;
  back?: boolean | undefined;
  backHref?: string | undefined;
  bodyClassName?: string | undefined;
  children: ReactNode;
}) {
  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="shrink-0 border-b border-border px-4 pb-4 pt-6 md:px-6">
        <PageHeader
          title={title}
          description={description}
          actions={actions}
          back={back}
          backHref={backHref}
          className="mb-0"
        />
      </div>
      <div className={cn("min-h-0 flex-1 overflow-y-auto px-4 pb-10 pt-6 md:px-6", bodyClassName)}>
        {children}
      </div>
    </div>
  );
}
