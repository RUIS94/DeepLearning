import type { Metadata } from "next";
import { OverrideDetailPage } from "./override-detail-page";

export const metadata: Metadata = {
  title: "标准修正详情",
};

export default function Page() {
  return <OverrideDetailPage />;
}
