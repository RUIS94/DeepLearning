import type { Metadata } from "next";
import { DeepLearningPage } from "./deep-learning-page";

export const metadata: Metadata = {
  title: "深入学习",
  description: "参考译文对照、句型拆解与词汇表达卡片，按题目缓存复用。",
  openGraph: {
    title: "深入学习 · 译练",
    description: "参考译文对照、句型拆解与词汇表达卡片。",
  },
};

export default function Page() {
  return <DeepLearningPage />;
}
