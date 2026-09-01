import type { Metadata } from "next";
import { ExamManagementPage } from "./exam-management-page";

export const metadata: Metadata = { title: "考试管理" };

export default function Page() {
  return <ExamManagementPage />;
}
