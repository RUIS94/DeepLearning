import type { Metadata } from "next";
import { OverrideDetailPage } from "./override-detail-page";

export const metadata: Metadata = {
  title: "Standard Revision Detail",
};

export default function Page() {
  return <OverrideDetailPage />;
}
