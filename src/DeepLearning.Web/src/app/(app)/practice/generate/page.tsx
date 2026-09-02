import { redirect } from "next/navigation";

/** AI 出题不再是独立页面,改为题库页的「AI 出题」按钮触发的 SidePanel(AiGeneratePanel)。 */
export default function Page() {
  redirect("/practice");
}
