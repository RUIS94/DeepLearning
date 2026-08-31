import type { Metadata } from "next";
import { ErrorTaxonomiesPage } from "./error-taxonomies-page";

export const metadata: Metadata = {
  title: "错误分类 · 管理后台",
};

export default function Page() {
  return <ErrorTaxonomiesPage />;
}
