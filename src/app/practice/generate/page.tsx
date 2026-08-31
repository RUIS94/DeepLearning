import type { Metadata } from "next";
import { GeneratePage } from "./generate-page";

export const metadata: Metadata = {
  title: "AI 出题",
  description: "按难度、题材与薄弱点定向生成中英笔译练习题目。",
  openGraph: {
    title: "AI 出题 · 译练",
    description: "按难度、题材与薄弱点定向生成练习题目。",
  },
};

export default function Page() {
  return <GeneratePage />;
}
