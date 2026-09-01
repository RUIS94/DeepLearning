"use client";

import { useRouter } from "next/navigation";
import type { ReactNode } from "react";
import { ArrowLeft } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";

/**
 * 页面标题区。左侧导航由 (app)/layout.tsx 的 AppSidebar 统一提供,页面自身只渲染这个头部。
 * back 为 true 时显示返回按钮(router.back());也可传 backHref 指定固定返回目标。
 */
export function PageHeader({
  title,
  description,
  actions,
  back,
  backHref,
  className,
}: {
  title: ReactNode;
  description?: ReactNode | undefined;
  actions?: ReactNode | undefined;
  back?: boolean | undefined;
  backHref?: string | undefined;
  className?: string | undefined;
}) {
  const router = useRouter();

  return (
    <div className={cn("mb-6 space-y-3", className)}>
      {back || backHref ? (
        <Button
          variant="ghost"
          size="sm"
          className="-ml-2 h-8 gap-1.5 px-2 text-muted-foreground"
          onClick={() => (backHref ? router.push(backHref) : router.back())}
        >
          <ArrowLeft className="size-4" />
          返回
        </Button>
      ) : null}
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
          {description ? (
            <p className="mt-1 max-w-3xl text-sm text-muted-foreground">{description}</p>
          ) : null}
        </div>
        {actions ? <div className="flex shrink-0 items-center gap-2">{actions}</div> : null}
      </div>
    </div>
  );
}
