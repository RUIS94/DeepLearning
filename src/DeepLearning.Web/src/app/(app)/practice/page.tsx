import type { Metadata } from "next";
import { PracticePage } from "./practice-page";

export const metadata: Metadata = {
  title: "Question Bank · Translation Practice",
  description:
    "Filter NAATI Chinese-English translation practice questions by task type, difficulty, and topic.",
  openGraph: {
    title: "Question Bank · Translation Practice",
    description:
      "Filter NAATI Chinese-English translation practice questions by task type, difficulty, and topic.",
  },
};

export default function Page() {
  return <PracticePage />;
}
