"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import {
  ArrowLeft,
  BookMarked,
  FileJson,
  FolderTree,
  GraduationCap,
  ShieldAlert,
  Sliders,
  Upload,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { examType } from "@/lib/mock/store";

const NAV = [
  { href: "/admin/exam-types", label: "考试类型", icon: GraduationCap },
  { href: `/admin/exam-types/${examType.id}/dimensions`, label: "评分维度", icon: Sliders },
  {
    href: `/admin/exam-types/${examType.id}/error-taxonomies`,
    label: "错误分类",
    icon: ShieldAlert,
  },
  { href: "/admin/prompt-templates", label: "Prompt 模板", icon: FileJson },
  { href: "/admin/llm-providers", label: "AI 供应商", icon: BookMarked },
  { href: "/admin/question-bank-categories", label: "题库分类", icon: FolderTree },
  { href: "/admin/questions/import", label: "导入题目", icon: Upload },
] as const;

/**
 * 内容管理后台布局（方案 §7.2）。
 * 与学生端 AppShell 分离——admin 目前只是前端约定俗成的保护（无真实角色系统，见方案 §16 第 4 点），
 * 视觉上做区分（顶部提示条）以避免误当成学生端页面。
 */
export function AdminShell({
  title,
  description,
  actions,
  children,
}: {
  title: string;
  description?: string | undefined;
  actions?: ReactNode;
  children: ReactNode;
}) {
  const pathname = usePathname();

  return (
    <div className="min-h-screen bg-background">
      <div className="bg-primary/10 px-4 py-1.5 text-center text-xs text-primary">
        内容管理后台 · 仅供内部使用，当前无角色鉴权保护（见开发方案 §16）
      </div>
      <header className="sticky top-0 z-40 border-b border-border bg-background/85 backdrop-blur">
        <div className="mx-auto flex h-14 max-w-6xl items-center gap-6 px-4">
          <Link
            href="/practice"
            className="flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground"
          >
            <ArrowLeft className="size-4" />
            返回学生端
          </Link>
          <nav className="flex flex-1 items-center gap-1 overflow-x-auto">
            {NAV.map((item) => {
              const isActive = pathname === item.href || pathname.startsWith(`${item.href}/`);
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={cn(
                    "inline-flex items-center gap-1.5 whitespace-nowrap rounded-md px-3 py-1.5 text-sm text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground",
                    isActive && "bg-secondary font-medium text-foreground",
                  )}
                >
                  <item.icon className="size-3.5" />
                  {item.label}
                </Link>
              );
            })}
          </nav>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 py-8">
        <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{title}</h1>
            {description ? (
              <p className="mt-1 max-w-2xl text-sm text-muted-foreground">{description}</p>
            ) : null}
          </div>
          {actions ? <div className="flex items-center gap-2">{actions}</div> : null}
        </div>
        {children}
      </main>
    </div>
  );
}
