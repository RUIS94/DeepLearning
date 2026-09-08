import { BookOpen, GraduationCap, LibraryBig, LineChart } from "lucide-react";
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
 */
export type NavItem = {
  kind: "link";
  key: string;
  labelKey: MessageKey;
  href: string;
  icon: LucideIcon;
  match: string;
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
];
