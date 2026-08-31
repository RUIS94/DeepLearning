"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ArrowRight, Scale } from "lucide-react";
import { AppShell } from "@/components/shared/app-shell";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { listStandardOverrides } from "@/lib/mock/store";
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

export function StandardOverridesPage() {
  const overrides = useQuery({
    queryKey: ["standard-overrides"],
    queryFn: () => listStandardOverrides(),
  });

  return (
    <AppShell
      title="标准修正记录"
      description="追问引发的评分标准 / 参考译文修正审计追溯，只读。每条修正先进入观察期，达到确认门槛后生效。"
    >
      {overrides.isPending ? (
        <div className="space-y-4">
          {[0, 1].map((i) => (
            <Skeleton key={i} className="h-24 w-full rounded-xl" />
          ))}
        </div>
      ) : overrides.data?.length ? (
        <div className="space-y-4">
          {overrides.data.map((o) => (
            <Link key={o.id} href={`/standard-overrides/${o.id}`} className="block">
              <Card className="border-border shadow-none transition-shadow hover:shadow-[var(--shadow-paper)]">
                <CardContent className="flex flex-wrap items-start justify-between gap-4 p-5">
                  <div className="min-w-64 flex-1 space-y-2">
                    <div className="flex flex-wrap items-center gap-2">
                      <Scale className="size-4 text-muted-foreground" />
                      <span className="text-sm font-medium">{scopeLabel[o.scope]}</span>
                      <Badge variant="outline" className="border-border text-muted-foreground">
                        {o.dimensionOrRule}
                      </Badge>
                      <Badge
                        variant="outline"
                        className={cn("border-transparent", statusTone[o.status])}
                      >
                        {OverrideStatusLabel[o.status]}
                      </Badge>
                    </div>
                    <p className="line-clamp-2 text-sm leading-relaxed text-muted-foreground">
                      {o.revisedRuleText}
                    </p>
                    <p className="text-numeric text-xs text-muted-foreground">
                      {formatDate(o.createdAt)}
                    </p>
                  </div>
                  <ArrowRight className="mt-1 size-4 shrink-0 text-muted-foreground" />
                </CardContent>
              </Card>
            </Link>
          ))}
        </div>
      ) : (
        <p className="rounded-lg border border-dashed border-border p-12 text-center text-sm text-muted-foreground">
          暂无标准修正记录。
        </p>
      )}
    </AppShell>
  );
}
