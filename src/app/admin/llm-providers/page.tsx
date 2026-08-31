import type { Metadata } from "next";
import { LlmProvidersPage } from "./llm-providers-page";

export const metadata: Metadata = {
  title: "AI 供应商 · 管理后台",
};

export default function Page() {
  return <LlmProvidersPage />;
}
