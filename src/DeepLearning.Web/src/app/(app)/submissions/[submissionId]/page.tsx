import type { Metadata } from "next";
import { SubmissionPage } from "./submission-page";

export const metadata: Metadata = {
  title: "Grading Result",
  description:
    "View the three-dimension Band scores and error list, and start a follow-up review of the verdict.",
  openGraph: {
    title: "Grading Result",
    description:
      "View the dimension scores and error list, and start a follow-up review of the verdict.",
  },
};

export default function Page() {
  return <SubmissionPage />;
}
