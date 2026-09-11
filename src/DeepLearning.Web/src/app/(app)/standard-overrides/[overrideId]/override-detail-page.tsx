"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, CheckCircle2, Gavel } from "lucide-react";
import { AppShell } from "@/components/shared/app-shell";
import { ErrorBanner } from "@/components/shared/ai-loading-state";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { activateStandardOverride, getStandardOverrideById } from "@/lib/api/standard-overrides";
import { OverrideScope, OverrideStatus } from "@/lib/types/enums";
import { formatDate } from "@/lib/band";
import { cn } from "@/lib/utils";
import { useT } from "@/lib/i18n";
import { useEnumLabels } from "@/lib/i18n/enum-labels";
import { qk } from "@/lib/query-keys";
import { overrideStatusTone } from "@/lib/enum-tone";

export function OverrideDetailPage() {
  const t = useT();
  const { OverrideStatusLabel } = useEnumLabels();
  const scopeLabel: Record<number, string> = {
    [OverrideScope.grading_rubric]: t("overrideDetail.scopeRubric"),
    [OverrideScope.translation_reference]: t("overrideDetail.scopeReference"),
  };
  const { overrideId } = useParams<{ overrideId: string }>();
  const queryClient = useQueryClient();
  const override = useQuery({
    queryKey: qk.standardOverride(overrideId),
    queryFn: () => getStandardOverrideById(overrideId),
  });
  const activate = useMutation({
    mutationFn: () => activateStandardOverride(overrideId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: qk.standardOverride(overrideId) }),
  });

  return (
    <AppShell
      title={t("overrideDetail.title")}
      actions={
        <Link
          href="/standard-overrides"
          className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="size-4" />
          {t("overrideDetail.backToList")}
        </Link>
      }
    >
      {override.isPending ? (
        <Skeleton className="h-64 w-full rounded-xl" />
      ) : override.isError || !override.data ? (
        <ErrorBanner error={override.error} />
      ) : (
        <Card className="max-w-2xl border-border shadow-none">
          <CardHeader>
            <div className="flex flex-wrap items-center gap-2">
              <Gavel className="size-4 text-muted-foreground" />
              <CardTitle className="text-base">
                {t("overrideDetail.revisionSuffix", {
                  scope: scopeLabel[override.data.scope] ?? "",
                })}
              </CardTitle>
              <Badge
                variant="outline"
                className={cn("border-transparent", overrideStatusTone[override.data.status])}
              >
                {OverrideStatusLabel[override.data.status]}
              </Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm text-muted-foreground">
              {t("overrideDetail.associatedRule")}{" "}
              <span className="text-foreground">{override.data.dimensionOrRule}</span>
            </p>
            {override.data.originalRuleText ? (
              <div className="space-y-1">
                <p className="text-xs font-medium text-muted-foreground">
                  {t("overrideDetail.before")}
                </p>
                <p className="text-sm leading-relaxed text-muted-foreground line-through opacity-70">
                  {override.data.originalRuleText}
                </p>
              </div>
            ) : null}
            <div className="space-y-1">
              <p className="text-xs font-medium text-muted-foreground">
                {t("overrideDetail.after")}
              </p>
              <p className="text-sm leading-relaxed">{override.data.revisedRuleText}</p>
            </div>
            <div className="text-numeric flex flex-wrap gap-x-6 gap-y-1 border-t border-border pt-4 text-xs text-muted-foreground">
              <span>
                {t("overrideDetail.created", { date: formatDate(override.data.createdAt) })}
              </span>
              {override.data.effectiveFrom ? (
                <span>
                  {t("overrideDetail.effective", { date: formatDate(override.data.effectiveFrom) })}
                </span>
              ) : null}
              {override.data.triggeredByFollowupId ? (
                <span>
                  {t("overrideDetail.triggeredBy", { id: override.data.triggeredByFollowupId })}
                </span>
              ) : null}
            </div>
            {override.data.status === OverrideStatus.observing ? (
              <div className="space-y-2 border-t border-border pt-4">
                <Button
                  size="sm"
                  variant="outline"
                  disabled={activate.isPending}
                  onClick={() => activate.mutate()}
                >
                  <CheckCircle2 className="size-4" />
                  {activate.isPending ? t("overrideDetail.approving") : t("overrideDetail.approve")}
                </Button>
                <p className="text-xs text-muted-foreground">{t("overrideDetail.approveHint")}</p>
                {activate.isError ? <ErrorBanner error={activate.error} /> : null}
              </div>
            ) : null}
          </CardContent>
        </Card>
      )}
    </AppShell>
  );
}
