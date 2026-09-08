import type { Metadata } from "next";
import { OverrideDetailPage } from "./override-detail-page";
import { getServerLocale, serverT } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  const t = serverT(await getServerLocale());
  return { title: t("meta.overrideDetail.title") };
}

export default function Page() {
  return <OverrideDetailPage />;
}
