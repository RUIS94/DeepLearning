"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import {
  BookOpen,
  GraduationCap,
  LineChart,
  Library,
  Settings,
  Sparkles,
  Target,
  Scale,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { MOCK_USER } from "@/lib/mock/store";

const NAV = [
  { href: "/practice", label: "练习", icon: BookOpen, exact: true },
  { href: "/practice/generate", label: "AI 出题", icon: Sparkles, exact: false },
  { href: "/review-library", label: "复习库", icon: Library, exact: false },
  { href: "/weak-points", label: "薄弱点", icon: Target, exact: false },
  { href: "/progress", label: "进度", icon: LineChart, exact: false },
  { href: "/standard-overrides", label: "标准修正", icon: Scale, exact: false },
] as const;

export function AppShell({
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
      <header className="sticky top-0 z-40 border-b border-border bg-background/85 backdrop-blur">
        <div className="mx-auto flex h-14 max-w-6xl items-center gap-6 px-4">
          <Link href="/" className="flex items-center gap-2 font-serif text-base font-semibold">
            <GraduationCap className="size-5 text-primary" />
            译练
          </Link>
          <nav className="flex flex-1 items-center gap-1 overflow-x-auto">
            {NAV.map((item) => {
              const isActive = item.exact ? pathname === item.href : pathname.startsWith(item.href);
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={cn(
                    "rounded-md px-3 py-1.5 text-sm text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground",
                    isActive && "bg-secondary font-medium text-foreground",
                  )}
                >
                  {item.label}
                </Link>
              );
            })}
          </nav>
          <div className="hidden shrink-0 items-center gap-3 sm:flex">
            <Link
              href="/admin"
              title="内容管理后台"
              className="text-muted-foreground transition-colors hover:text-foreground"
            >
              <Settings className="size-4" />
            </Link>
            <span className="text-xs text-muted-foreground">{MOCK_USER.email}</span>
            <span className="flex size-8 items-center justify-center rounded-full bg-primary text-xs font-medium text-primary-foreground">
              {MOCK_USER.name.slice(0, 1)}
            </span>
          </div>
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
