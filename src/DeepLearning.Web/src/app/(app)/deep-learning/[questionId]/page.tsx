import type { Metadata } from "next";
import { DeepLearningPage } from "./deep-learning-page";

export const metadata: Metadata = {
  title: "Deep Learning",
  description:
    "Reference-translation comparison, sentence breakdowns, and vocabulary cards, cached per question.",
  openGraph: {
    title: "Deep Learning",
    description: "Reference-translation comparison, sentence breakdowns, and vocabulary cards.",
  },
};

export default function Page() {
  return <DeepLearningPage />;
}
