"use client";

import { PageShell } from "@/components/shell/page-shell";
import { Card, CardContent } from "@/components/ui/card";
import { useCurrentUser } from "@/hooks/use-current-user";
import { useT } from "@/lib/i18n";

export default function ProfilePage() {
  const t = useT();
  const currentUser = useCurrentUser();

  return (
    <PageShell
      title={t("profile.title")}
      description={t("profile.description")}
      back
      backHref="/practice"
    >
      <Card className="max-w-xl border-border shadow-none">
        <CardContent className="space-y-4 p-6 text-sm">
          <div className="grid grid-cols-[6rem_1fr] gap-x-4 gap-y-3">
            <span className="text-muted-foreground">{t("profile.displayName")}</span>
            <span>{currentUser.data?.displayName ?? t("common.none")}</span>
            <span className="text-muted-foreground">{t("profile.email")}</span>
            <span>{currentUser.data?.email ?? t("common.none")}</span>
          </div>
          <p className="border-t border-border pt-4 text-xs text-muted-foreground">
            {t("profile.placeholder")}
          </p>
        </CardContent>
      </Card>
    </PageShell>
  );
}
