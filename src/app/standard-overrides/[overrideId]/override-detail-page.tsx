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
import { OverrideScope, OverrideStatus, OverrideStatusLabel } from "@/lib/types/enums";
import { formatDate } from "@/lib/band";
import { cn } from "@/lib/utils";

const scopeLabel: Record<number, string> = {
  [OverrideScope.grading_rubric]: "评分标准",
  [OverrideScope.translation_reference]: "参考译文",
};

const statusTone: Record<number, string> = {
  [OverrideStatus.observing]: "bg-warning/20 text-warning-foreground",
  [OverrideStatus.active]: "bg-success/12 text-success",
  [OverrideStatus.deprecated]: "bg-muted text-muted-foreground",
};

export function OverrideDetailPage() {
  const { overrideId } = useParams<{ overrideId: string }>();
  const queryClient = useQueryClient();
  const override = useQuery({
    queryKey: ["standard-override", overrideId],
    queryFn: () => getStandardOverrideById(overrideId),
  });
  const activate = useMutation({
    mutationFn: () => activateStandardOverride(overrideId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["standard-override", overrideId] }),
  });

  return (
    <AppShell
      title="标准修正详情"
      actions={
        <Link
          href="/standard-overrides"
          className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="size-4" />
          返回列表
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
              <CardTitle className="text-base">{scopeLabel[override.data.scope]}修正</CardTitle>
              <Badge
                variant="outline"
                className={cn("border-transparent", statusTone[override.data.status])}
              >
                {OverrideStatusLabel[override.data.status]}
              </Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm text-muted-foreground">
              关联维度/规则：
              <span className="text-foreground">{override.data.dimensionOrRule}</span>
            </p>
            {override.data.originalRuleText ? (
              <div className="space-y-1">
                <p className="text-xs font-medium text-muted-foreground">修订前</p>
                <p className="text-sm leading-relaxed text-muted-foreground line-through opacity-70">
                  {override.data.originalRuleText}
                </p>
              </div>
            ) : null}
            <div className="space-y-1">
              <p className="text-xs font-medium text-muted-foreground">修订后</p>
              <p className="text-sm leading-relaxed">{override.data.revisedRuleText}</p>
            </div>
            <div className="text-numeric flex flex-wrap gap-x-6 gap-y-1 border-t border-border pt-4 text-xs text-muted-foreground">
              <span>生成时间 {formatDate(override.data.createdAt)}</span>
              {override.data.effectiveFrom ? (
                <span>生效时间 {formatDate(override.data.effectiveFrom)}</span>
              ) : null}
              {override.data.triggeredByFollowupId ? (
                <span>由追问 {override.data.triggeredByFollowupId} 触发</span>
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
                  {activate.isPending ? "核准中…" : "人工核准生效"}
                </Button>
                <p className="text-xs text-muted-foreground">
                  design doc §10.6：无需等待累计确认次数达标，经一次人工复核即可直接生效。
                </p>
                {activate.isError ? <ErrorBanner error={activate.error} /> : null}
              </div>
            ) : null}
          </CardContent>
        </Card>
      )}
    </AppShell>
  );
}
