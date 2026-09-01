import type { Metadata } from "next";
import { ExamTypeConfigPage } from "./exam-type-config-page";

export const metadata: Metadata = { title: "考试配置" };

export default function Page() {
  return <ExamTypeConfigPage />;
}
