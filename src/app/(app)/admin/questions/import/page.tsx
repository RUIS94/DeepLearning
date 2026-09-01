import { redirect } from "next/navigation";

/** 导入题目改为侧栏「导入题目」触发的 SidePanel(ImportPanelProvider),不再是独立页面。 */
export default function Page() {
  redirect("/practice");
}
