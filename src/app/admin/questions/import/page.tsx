import type { Metadata } from "next";
import { ImportQuestionPage } from "./import-question-page";

export const metadata: Metadata = {
  title: "导入题目 · 管理后台",
};

export default function Page() {
  return <ImportQuestionPage />;
}
