import type { Metadata } from "next";
import { ResetPasswordPage } from "./reset-password-page";
import { getServerLocale, serverT } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  const t = serverT(await getServerLocale());
  return {
    title: t("meta.resetPassword.title"),
    description: t("meta.resetPassword.description"),
    openGraph: {
      title: t("meta.resetPassword.title"),
      description: t("meta.resetPassword.description"),
    },
  };
}

export default function Page() {
  return <ResetPasswordPage />;
}
