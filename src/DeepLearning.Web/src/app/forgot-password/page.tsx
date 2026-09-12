import type { Metadata } from "next";
import { ForgotPasswordPage } from "./forgot-password-page";
import { getServerLocale, serverT } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  const t = serverT(await getServerLocale());
  return {
    title: t("meta.forgotPassword.title"),
    description: t("meta.forgotPassword.description"),
    openGraph: {
      title: t("meta.forgotPassword.title"),
      description: t("meta.forgotPassword.description"),
    },
  };
}

export default function Page() {
  return <ForgotPasswordPage />;
}
