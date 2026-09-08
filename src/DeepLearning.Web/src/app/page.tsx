import type { Metadata } from "next";
import { LoginPage } from "./login-page";
import { getServerLocale, serverT } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  const t = serverT(await getServerLocale());
  return {
    title: { absolute: t("meta.home.title") },
    description: t("meta.home.description"),
    openGraph: {
      title: t("meta.home.title"),
      description: t("meta.home.ogDescription"),
    },
  };
}

export default function Page() {
  return <LoginPage />;
}
