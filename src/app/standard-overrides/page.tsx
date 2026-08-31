import type { Metadata } from "next";
import { StandardOverridesPage } from "./standard-overrides-page";

export const metadata: Metadata = {
  title: "标准修正记录",
  description: "追问引发的评分标准修正记录审计追溯。",
  openGraph: {
    title: "标准修正记录 · 译练",
    description: "追问引发的评分标准修正记录审计追溯。",
  },
};

export default function Page() {
  return <StandardOverridesPage />;
}
