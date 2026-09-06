import type { Metadata } from "next";
import { ProgressPage } from "./progress-page";

export const metadata: Metadata = {
  title: "Learning Progress",
  description: "Three-dimensional Band trends and pass rate dashboard, with AI trend commentary.",
  openGraph: {
    title: "Learning Progress · Deep Learning",
    description: "Three-dimensional Band trends and pass rate dashboard, with AI trend commentary.",
  },
};

export default function Page() {
  return <ProgressPage />;
}
