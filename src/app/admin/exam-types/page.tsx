import type { Metadata } from "next";
import { ExamTypesPage } from "./exam-types-page";

export const metadata: Metadata = {
  title: "考试类型 · 管理后台",
};

export default function Page() {
  return <ExamTypesPage />;
}
