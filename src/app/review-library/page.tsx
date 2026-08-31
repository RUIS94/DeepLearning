import type { Metadata } from "next";
import { ReviewLibraryPage } from "./review-library-page";

export const metadata: Metadata = {
  title: "复习库",
  description: "跨题目沉淀的句型与词汇，按题材、场景与掌握程度筛选复习。",
  openGraph: {
    title: "复习库 · 译练",
    description: "跨题目沉淀的句型与词汇复习库。",
  },
};

export default function Page() {
  return <ReviewLibraryPage />;
}
