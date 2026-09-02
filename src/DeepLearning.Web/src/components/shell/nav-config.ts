import { BookOpen, GraduationCap, LibraryBig, LineChart } from "lucide-react";
import type { LucideIcon } from "lucide-react";

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
  label: string;
  href: string;
  icon: LucideIcon;
  match: string;
};

export const NAV_ITEMS: NavItem[] = [
  {
    kind: "link",
    key: "practice",
    label: "题库",
    href: "/practice",
    icon: BookOpen,
    match: "/practice",
  },
  {
    kind: "link",
    key: "exam-management",
    label: "考试管理",
    href: "/exam-management",
    icon: GraduationCap,
    match: "/exam-management",
  },
  {
    kind: "link",
    key: "review",
    label: "复习",
    href: "/review",
    icon: LibraryBig,
    match: "/review",
  },
  {
    kind: "link",
    key: "progress",
    label: "进度",
    href: "/progress",
    icon: LineChart,
    match: "/progress",
  },
];
