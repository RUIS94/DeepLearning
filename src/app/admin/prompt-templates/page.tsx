import type { Metadata } from "next";
import { PromptTemplatesPage } from "./prompt-templates-page";

export const metadata: Metadata = {
  title: "Prompt 模板 · 管理后台",
};

export default function Page() {
  return <PromptTemplatesPage />;
}
