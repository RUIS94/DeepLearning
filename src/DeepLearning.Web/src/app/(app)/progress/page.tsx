import type { Metadata } from "next";
import { ProgressPage } from "./progress-page";
import { getServerLocale, serverT } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  const t = serverT(await getServerLocale());
  return {
    title: t("meta.progress.title"),
    description: t("meta.progress.description"),
    openGraph: {
      title: t("meta.progress.title"),
      description: t("meta.progress.description"),
    },
  };
}

export default function Page() {
  return <ProgressPage />;
}
