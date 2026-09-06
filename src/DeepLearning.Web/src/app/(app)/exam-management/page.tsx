import type { Metadata } from "next";
import { ExamManagementPage } from "./exam-management-page";

export const metadata: Metadata = { title: "Management" };

export default function Page() {
  return <ExamManagementPage />;
}
