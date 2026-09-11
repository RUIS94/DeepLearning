"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Sparkles } from "lucide-react";
import { AppShell } from "@/components/shared/app-shell";
import { BandTrendChart } from "@/components/progress/band-trend-chart";
import { PassRateChart } from "@/components/progress/pass-rate-chart";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/shared/empty-state";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { listProgress } from "@/lib/api/progress";
import { useCurrentUser } from "@/hooks/use-current-user";
import { useT } from "@/lib/i18n";
import { formatDate } from "@/lib/band";
import { qk } from "@/lib/query-keys";

const ALL = "all";

export function ProgressPage() {
  const t = useT();
  const [difficultyTier, setDifficultyTier] = useState(ALL);
  const currentUser = useCurrentUser();

  const snapshots = useQuery({
    queryKey: qk.progress(currentUser.data?.id, difficultyTier),
    queryFn: () =>
      listProgress(currentUser.data!.id, difficultyTier === ALL ? undefined : difficultyTier),
    enabled: !!currentUser.data,
  });

  const latest = snapshots.data?.[snapshots.data.length - 1];

  return (
    <AppShell
      title={t("progress.title")}
      description={t("progress.description")}
      actions={
        <Select value={difficultyTier} onValueChange={setDifficultyTier}>
          <SelectTrigger className="w-36">
            <SelectValue placeholder={t("progress.filter.difficultyTier")} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>{t("progress.filter.allDifficulties")}</SelectItem>
            <SelectItem value="easy">{t("progress.tier.easy")}</SelectItem>
            <SelectItem value="medium">{t("progress.tier.medium")}</SelectItem>
            <SelectItem value="hard">{t("progress.tier.hard")}</SelectItem>
          </SelectContent>
        </Select>
      }
    >
      {snapshots.isPending ? (
        <div className="space-y-6">
          <Skeleton className="h-72 w-full rounded-xl" />
          <Skeleton className="h-56 w-full rounded-xl" />
        </div>
      ) : snapshots.data?.length ? (
        <div className="space-y-6">
          <Card className="border-border shadow-none">
            <CardHeader>
              <CardTitle className="text-base">{t("progress.bandTrendTitle")}</CardTitle>
            </CardHeader>
            <CardContent>
              <BandTrendChart snapshots={snapshots.data} />
            </CardContent>
          </Card>

          <Card className="border-border shadow-none">
            <CardHeader>
              <CardTitle className="text-base">{t("progress.passRateTitle")}</CardTitle>
            </CardHeader>
            <CardContent>
              <PassRateChart snapshots={snapshots.data} />
            </CardContent>
          </Card>

          {latest?.trendNote ? (
            <div className="flex items-start gap-3 rounded-lg border border-primary/30 bg-primary/5 p-4">
              <Sparkles className="mt-0.5 size-4 shrink-0 text-primary" />
              <div className="space-y-1">
                <p className="text-sm font-medium">
                  {t("progress.latestCommentary", {
                    start: formatDate(latest.periodStart),
                    end: formatDate(latest.periodEnd),
                  })}
                </p>
                <p className="text-sm leading-relaxed text-muted-foreground">{latest.trendNote}</p>
              </div>
            </div>
          ) : null}
        </div>
      ) : (
        <EmptyState>{t("progress.empty")}</EmptyState>
      )}
    </AppShell>
  );
}
