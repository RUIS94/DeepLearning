import type { Metadata } from "next";
import { WeakPointsPage } from "./weak-points-page";

export const metadata: Metadata = {
  title: "薄弱点",
  description: "AI 自动归类的翻译薄弱点追踪，只读展示。",
  openGraph: {
    title: "薄弱点 · 译练",
    description: "AI 自动归类的翻译薄弱点追踪。",
  },
};

export default function Page() {
  return <WeakPointsPage />;
}
