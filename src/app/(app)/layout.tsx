import type { ReactNode } from "react";
import { AppSidebar } from "@/components/shell/app-sidebar";
import { ImportPanelProvider } from "@/components/practice/import-question-panel";
import { SidebarInset, SidebarProvider, SidebarTrigger } from "@/components/ui/sidebar";
import ToastStack from "@/components/ui/toast";

/**
 * 登录后的应用外壳:左侧可折叠导航 + 全宽主内容(不做两侧内缩/居中)。
 *
 * 高度锁定为视口(h-svh + overflow-hidden),不产生 body 级滚动 —— 页面自己在
 * AppShell/AdminShell 里把标题区固定、只让内容区滚动(见那两个组件)。
 * 登录页(/ → app/page.tsx)不在这个路由组里,所以没有侧栏。
 */
export default function AppLayout({ children }: { children: ReactNode }) {
  return (
    <SidebarProvider className="h-svh min-h-0 overflow-hidden">
      <ImportPanelProvider>
        <AppSidebar />
        <SidebarInset className="min-w-0 overflow-hidden">
          <header className="flex h-12 shrink-0 items-center gap-2 border-b border-border px-3">
            <SidebarTrigger />
          </header>
          <div className="flex min-h-0 flex-1 flex-col overflow-hidden">{children}</div>
        </SidebarInset>
      </ImportPanelProvider>
      <ToastStack />
    </SidebarProvider>
  );
}
