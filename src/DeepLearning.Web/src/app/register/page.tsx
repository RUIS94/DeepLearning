import type { Metadata } from "next";
import { RegisterPage } from "./register-page";
import { getServerLocale, serverT } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  const t = serverT(await getServerLocale());
  return {
    title: t("meta.register.title"),
    description: t("meta.register.description"),
    openGraph: {
      title: t("meta.register.title"),
      description: t("meta.register.description"),
    },
  };
}

export default function Page() {
  return <RegisterPage />;
}
