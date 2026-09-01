import type { Metadata } from "next";
import { PracticePage } from "./practice-page";

export const metadata: Metadata = {
  title: "题库浏览",
  description: "按任务类型、难度与题材筛选 NAATI 中英笔译练习题目。",
  openGraph: {
    title: "题库浏览 · 译练",
    description: "按任务类型、难度与题材筛选中英笔译练习题目。",
  },
};

export default function Page() {
  return <PracticePage />;
}
