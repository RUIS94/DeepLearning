import type { Metadata } from "next";
import { LoginPage } from "./login-page";

export const metadata: Metadata = {
  title: { absolute: "Deep Learning · Chinese-English Translation Practice & AI Grading" },
  description:
    "A translation practice platform for NAATI-certified interpreters: authentic exam questions, TaskA translation and TaskB error annotation, AI-powered dimension-based grading, follow-up review, and learning curves.",
  openGraph: {
    title: "Deep Learning · Chinese-English Translation Practice & AI Grading",
    description: "Authentic practice, AI-powered dimension-based grading, and weak point tracking for improving Chinese-English interpretation skills.",
  },
};

export default function Page() {
  return <LoginPage />;
}
