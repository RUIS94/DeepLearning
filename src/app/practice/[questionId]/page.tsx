import type { Metadata } from "next";
import { AnswerPage } from "./answer-page";

export const metadata: Metadata = {
  title: "答题",
  description: "阅读原文完成翻译或找错标注，提交后由 AI 分维度批改。",
  openGraph: {
    title: "答题 · 译练",
    description: "完成翻译或找错标注，提交后由 AI 分维度批改。",
  },
};

export default function Page() {
  return <AnswerPage />;
}
