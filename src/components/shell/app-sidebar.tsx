"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { GraduationCap } from "lucide-react";
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
} from "@/components/ui/sidebar";
import { UserMenu } from "@/components/shell/user-menu";
import { NAV_ITEMS } from "@/components/shell/nav-config";
import { useImportPanel } from "@/components/practice/import-question-panel";

export function AppSidebar() {
  const pathname = usePathname();
  const importPanel = useImportPanel();

  return (
    <Sidebar collapsible="icon">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" asChild>
              <Link href="/practice">
                <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary text-primary-foreground">
                  <GraduationCap className="size-4" />
                </span>
                <span className="font-serif text-base font-semibold">译练</span>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>

      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <SidebarMenu>
              {NAV_ITEMS.map((item) => {
                if (item.kind === "panel") {
                  return (
                    <SidebarMenuItem key={item.key}>
                      <SidebarMenuButton tooltip={item.label} onClick={() => importPanel.open()}>
                        <item.icon />
                        <span>{item.label}</span>
                      </SidebarMenuButton>
                    </SidebarMenuItem>
                  );
                }
                const active =
                  pathname === item.href ||
                  pathname.startsWith(`${item.match}/`) ||
                  pathname === item.match;
                return (
                  <SidebarMenuItem key={item.key}>
                    <SidebarMenuButton tooltip={item.label} isActive={active} asChild>
                      <Link href={item.href}>
                        <item.icon />
                        <span>{item.label}</span>
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
