import type { Metadata } from "next";
import { SubmissionPage } from "./submission-page";
import { getServerLocale, serverT } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  const t = serverT(await getServerLocale());
  return {
    title: t("meta.submission.title"),
    description: t("meta.submission.description"),
    openGraph: {
      title: t("meta.submission.title"),
      description: t("meta.submission.ogDescription"),
    },
  };
}

export default function Page() {
  return <SubmissionPage />;
}
