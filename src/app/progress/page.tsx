import type { Metadata } from "next";
import { ProgressPage } from "./progress-page";

export const metadata: Metadata = {
  title: "学习曲线",
  description: "三维度 Band 趋势与通过率仪表盘，含 AI 趋势点评。",
  openGraph: {
    title: "学习曲线 · 译练",
    description: "三维度 Band 趋势与通过率仪表盘。",
  },
};

export default function Page() {
  return <ProgressPage />;
}
