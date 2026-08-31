"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Sparkles } from "lucide-react";
import { AppShell } from "@/components/shared/app-shell";
import { BandTrendChart } from "@/components/progress/band-trend-chart";
import { PassRateChart } from "@/components/progress/pass-rate-chart";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { listProgress } from "@/lib/mock/store";
import { formatDate } from "@/lib/band";

const ALL = "all";

export function ProgressPage() {
  const [difficultyTier, setDifficultyTier] = useState(ALL);

  const snapshots = useQuery({
    queryKey: ["progress", difficultyTier],
    queryFn: () => listProgress(difficultyTier === ALL ? undefined : difficultyTier),
  });

  const latest = snapshots.data?.[snapshots.data.length - 1];

  return (
    <AppShell
      title="学习曲线"
      description="按周期汇总的三维度 Band 均值与通过率，AI 会在关键节点附上趋势点评。"
      actions={
        <Select value={difficultyTier} onValueChange={setDifficultyTier}>
          <SelectTrigger className="w-36">
            <SelectValue placeholder="难度层级" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>全部难度</SelectItem>
            <SelectItem value="easy">简单</SelectItem>
            <SelectItem value="medium">中等</SelectItem>
            <SelectItem value="hard">困难</SelectItem>
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
              <CardTitle className="text-base">三维度 Band 趋势（数值越低越好）</CardTitle>
            </CardHeader>
            <CardContent>
              <BandTrendChart snapshots={snapshots.data} />
            </CardContent>
          </Card>

          <Card className="border-border shadow-none">
            <CardHeader>
              <CardTitle className="text-base">通过率趋势</CardTitle>
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
                  最新周期点评（{formatDate(latest.periodStart)} – {formatDate(latest.periodEnd)}）
                </p>
                <p className="text-sm leading-relaxed text-muted-foreground">{latest.trendNote}</p>
              </div>
            </div>
          ) : null}
        </div>
      ) : (
        <p className="rounded-lg border border-dashed border-border p-12 text-center text-sm text-muted-foreground">
          暂无学习曲线数据，累计更多提交后会自动生成周期快照。
        </p>
      )}
    </AppShell>
  );
}
