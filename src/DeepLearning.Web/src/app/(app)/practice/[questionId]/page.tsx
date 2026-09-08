import type { Metadata } from "next";
import { AnswerPage } from "./answer-page";
import { getServerLocale, serverT } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  const t = serverT(await getServerLocale());
  return {
    title: t("meta.answer.title"),
    description: t("meta.answer.description"),
    openGraph: {
      title: t("meta.answer.title"),
      description: t("meta.answer.ogDescription"),
    },
  };
}

export default function Page() {
  return <AnswerPage />;
}
