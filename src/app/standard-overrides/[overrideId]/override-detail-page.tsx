"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Gavel } from "lucide-react";
import { AppShell } from "@/components/shared/app-shell";
import { ErrorBanner } from "@/components/shared/ai-loading-state";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { getStandardOverrideById } from "@/lib/mock/store";
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
  const override = useQuery({
    queryKey: ["standard-override", overrideId],
    queryFn: () => getStandardOverrideById(overrideId),
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
            {override.data.dimensionKey ? (
              <p className="text-sm text-muted-foreground">
                关联维度：<span className="text-foreground">{override.data.dimensionKey}</span>
              </p>
            ) : null}
            <p className="text-sm leading-relaxed">{override.data.reason}</p>
            <div className="text-numeric flex flex-wrap gap-x-6 gap-y-1 border-t border-border pt-4 text-xs text-muted-foreground">
              <span>生成时间 {formatDate(override.data.createdAt)}</span>
              <Link
                href={`/submissions/${override.data.submissionId}`}
                className="text-primary underline underline-offset-2"
              >
                关联提交 {override.data.submissionId}
              </Link>
            </div>
          </CardContent>
        </Card>
      )}
    </AppShell>
  );
}
