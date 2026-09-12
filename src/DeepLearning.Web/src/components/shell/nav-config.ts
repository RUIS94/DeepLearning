import { BookOpen, GraduationCap, LibraryBig, LineChart, ShieldCheck } from "lucide-react";
import type { LucideIcon } from "lucide-react";
import type { MessageKey } from "@/lib/i18n/messages/en";

/**
 * 左侧导航项。"AI 出题"和"导入题目"都不在这里 —— 它们是题库页里的按钮触发的 SidePanel,
 * 不是独立路由。其余是普通路由跳转(kind: "link")。
 *
 * Phase 1 阶段 href 先指向现有路由;后续阶段会把
 *   /admin/exam-types  -> /exam-management(多 tab)
 *   /review-library    -> /review(并入薄弱点 tab)
 * 迁移过去,这里同步改即可,页面组件本身复用。
 *
 * adminOnly 项只在当前用户 role=admin 时渲染（AppSidebar 里过滤，见
 * ref/管理员与用户权限隔离_策划书.md）——真正的门禁在后端 AdminOnly policy，这里只是体验层面
 * 的隐藏，不是安全边界。
 */
export type NavItem = {
  kind: "link";
  key: string;
  labelKey: MessageKey;
  href: string;
  icon: LucideIcon;
  match: string;
  adminOnly?: boolean;
};

export const NAV_ITEMS: NavItem[] = [
  {
    kind: "link",
    key: "practice",
    labelKey: "nav.practice",
    href: "/practice",
    icon: BookOpen,
    match: "/practice",
  },
  {
    kind: "link",
    key: "exam-management",
    labelKey: "nav.examManagement",
    href: "/exam-management",
    icon: GraduationCap,
    match: "/exam-management",
  },
  {
    kind: "link",
    key: "review",
    labelKey: "nav.review",
    href: "/review",
    icon: LibraryBig,
    match: "/review",
  },
  {
    kind: "link",
    key: "progress",
    labelKey: "nav.progress",
    href: "/progress",
    icon: LineChart,
    match: "/progress",
  },
  {
    kind: "link",
    key: "admin",
    labelKey: "nav.admin",
    href: "/admin/users",
    icon: ShieldCheck,
    match: "/admin",
    adminOnly: true,
  },
];
