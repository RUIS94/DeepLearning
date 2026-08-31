import type { Metadata } from "next";
import { QuestionBankCategoriesPage } from "./question-bank-categories-page";

export const metadata: Metadata = {
  title: "题库分类 · 管理后台",
};

export default function Page() {
  return <QuestionBankCategoriesPage />;
}
