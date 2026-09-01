"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { ChevronsUpDown, LogOut, Settings, User } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import {
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from "@/components/ui/sidebar";
import { useCurrentUser } from "@/hooks/use-current-user";
import { getSupabaseBrowserClient } from "@/lib/auth/supabase-client";

/** nav 栏底部:头像 + 人名,点击展开 submenu(个人资料 / 设置 / 退出登录)。 */
export function UserMenu() {
  const router = useRouter();
  const { state } = useSidebar();
  const currentUser = useCurrentUser();
  const [logoutOpen, setLogoutOpen] = useState(false);

  const name = currentUser.data?.displayName ?? currentUser.data?.email?.split("@")[0] ?? "未登录";
  const email = currentUser.data?.email ?? "";
  const initial = (name || "?").slice(0, 1).toUpperCase();

  async function handleLogout() {
    const supabase = getSupabaseBrowserClient();
    if (supabase) await supabase.auth.signOut();
    router.push("/");
    router.refresh();
  }

  return (
    <>
      <SidebarMenu>
        <SidebarMenuItem>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <SidebarMenuButton
                size="lg"
                className="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground"
              >
                <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary text-xs font-medium text-primary-foreground">
                  {initial}
                </span>
                <span className="grid flex-1 text-left text-sm leading-tight">
                  <span className="truncate font-medium">{name}</span>
                  {email ? (
                    <span className="truncate text-xs text-muted-foreground">{email}</span>
                  ) : null}
                </span>
                <ChevronsUpDown className="ml-auto size-4" />
              </SidebarMenuButton>
            </DropdownMenuTrigger>
            <DropdownMenuContent
              side={state === "collapsed" ? "right" : "top"}
              align="start"
              className="w-56"
            >
              <DropdownMenuItem onSelect={() => router.push("/profile")}>
                <User className="size-4" />
                个人资料
              </DropdownMenuItem>
              <DropdownMenuItem onSelect={() => router.push("/settings")}>
                <Settings className="size-4" />
                设置
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                onSelect={(event) => {
                  event.preventDefault();
                  setLogoutOpen(true);
                }}
              >
                <LogOut className="size-4" />
                退出登录
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </SidebarMenuItem>
      </SidebarMenu>

      <ConfirmDialog
        open={logoutOpen}
        onOpenChange={setLogoutOpen}
        tone="warning"
        title="退出登录？"
        description="将结束当前会话，需要重新登录才能继续练习。"
        confirmLabel="退出登录"
        cancelLabel="取消"
        onConfirm={handleLogout}
      />
    </>
  );
}
