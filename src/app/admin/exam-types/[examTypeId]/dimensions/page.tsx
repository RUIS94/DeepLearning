import type { Metadata } from "next";
import { DimensionsPage } from "./dimensions-page";

export const metadata: Metadata = {
  title: "评分维度 · 管理后台",
};

export default function Page() {
  return <DimensionsPage />;
}
