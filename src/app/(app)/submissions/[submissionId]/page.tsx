import type { Metadata } from "next";
import { SubmissionPage } from "./submission-page";

export const metadata: Metadata = {
  title: "批改结果",
  description: "查看三维度 Band 评分、错误清单，并对判定发起追问复核。",
  openGraph: {
    title: "批改结果 · 译练",
    description: "查看维度评分与错误清单，并对判定发起追问复核。",
  },
};

export default function Page() {
  return <SubmissionPage />;
}
