"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { PanelLeft } from "lucide-react";
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
  SidebarTrigger,
  useSidebar,
} from "@/components/ui/sidebar";
import { UserMenu } from "@/components/shell/user-menu";
import { NAV_ITEMS } from "@/components/shell/nav-config";
import { useT } from "@/lib/i18n";

export function AppSidebar() {
  const t = useT();
  const pathname = usePathname();
  const { state, toggleSidebar } = useSidebar();
  const collapsed = state === "collapsed";

  return (
    <Sidebar collapsible="icon">
      <SidebarHeader>
        <div className="flex items-center justify-between gap-2 group-data-[collapsible=icon]:flex-col group-data-[collapsible=icon]:gap-1">
          {collapsed ? (
            <button
              type="button"
              onClick={toggleSidebar}
              aria-label="Expand sidebar"
              className="group/logo flex size-8 shrink-0 items-center justify-center rounded-lg text-sidebar-foreground hover:bg-sidebar-accent cursor-pointer data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-foreground"
            >
              {/* Static SVG in /public — next/image would be overkill for a fixed 32px logo. */}
              <img
                src="/logo.svg"
                alt="DeepLearning"
                className="size-8 rounded-lg group-hover/logo:hidden"
              />
              <PanelLeft className="hidden size-4 group-hover/logo:block" />
            </button>
          ) : (
            <>
              <Link href="/practice" className="flex min-w-0 items-center gap-2 rounded-md">
                {/* Static SVG in /public — next/image would be overkill for a fixed 32px logo. */}
                <img src="/logo.svg" alt="DeepLearning" className="size-8 shrink-0 rounded-lg" />
              </Link>
              <SidebarTrigger className="shrink-0" />
            </>
          )}
        </div>
      </SidebarHeader>

      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <SidebarMenu>
              {NAV_ITEMS.map((item) => {
                const active =
                  pathname === item.href ||
                  pathname.startsWith(`${item.match}/`) ||
                  pathname === item.match;
                const label = t(item.labelKey);
                return (
                  <SidebarMenuItem key={item.key}>
                    <SidebarMenuButton tooltip={label} isActive={active} asChild>
                      <Link href={item.href}>
                        <item.icon />
                        <span>{label}</span>
                      </Link>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                );
              })}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>

      <SidebarFooter>
        <UserMenu />
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>
  );
}
