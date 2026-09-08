import type { Metadata } from "next";
import { DeepLearningPage } from "./deep-learning-page";
import { getServerLocale, serverT } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  const t = serverT(await getServerLocale());
  return {
    title: t("meta.deepLearning.title"),
    description: t("meta.deepLearning.description"),
    openGraph: {
      title: t("meta.deepLearning.title"),
      description: t("meta.deepLearning.ogDescription"),
    },
  };
}

export default function Page() {
  return <DeepLearningPage />;
}
