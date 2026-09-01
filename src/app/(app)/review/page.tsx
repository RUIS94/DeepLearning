import type { Metadata } from "next";
import { ReviewPage } from "./review-page";

export const metadata: Metadata = { title: "复习" };

export default function Page() {
  return <ReviewPage />;
}
