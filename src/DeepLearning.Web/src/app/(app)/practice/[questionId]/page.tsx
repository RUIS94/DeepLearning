import type { Metadata } from "next";
import { AnswerPage } from "./answer-page";

export const metadata: Metadata = {
  title: "Answer",
  description:
    "Read the source text, complete the translation or annotate errors, then get AI grading by dimension.",
  openGraph: {
    title: "Answer",
    description: "Complete the translation or annotate errors, then get AI grading by dimension.",
  },
};

export default function Page() {
  return <AnswerPage />;
}
