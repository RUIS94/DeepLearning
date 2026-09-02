"use client";

import { AlertCircle, CheckCircle2, Flame } from "lucide-react";
import type { SubmissionDetail } from "@/lib/types/dtos";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { bandLabel, bandToColor } from "@/lib/band";
import { cn } from "@/lib/utils";

function DimensionBandRow({
  name,
  band,
  pass,
  rationale,
  densityNote,
  probability,
}: {
  name: string;
  band: number;
  pass: boolean;
  rationale: string;
  densityNote: string | null;
  probability: number | null;
}) {
  return (
    <div className="space-y-2 border-b border-border py-4 last:border-0">
      <div className="flex flex-wrap items-center gap-3">
        <span
          className="text-numeric flex size-9 items-center justify-center rounded-md text-sm font-semibold text-primary-foreground"
          style={{ backgroundColor: bandToColor(band) }}
        >
          {band}
        </span>
        <div className="flex-1">
          <p className="text-sm font-medium">{name}</p>
          <p className="text-xs text-muted-foreground">
            Band {band} · {bandLabel(band)}
            {probability !== null
              ? ` · 预估通过概率 ${Math.round((probability > 1 ? probability / 100 : probability) * 100)}%`
              : ""}
          </p>
        </div>
        <Badge
          variant="outline"
          className={cn(
            "border-transparent",
            pass ? "bg-success/12 text-success" : "bg-destructive/12 text-destructive",
          )}
        >
          {pass ? "达标" : "未达标"}
        </Badge>
      </div>
      <p className="text-sm leading-relaxed text-muted-foreground">{rationale}</p>
      {densityNote ? (
        <p className="inline-flex items-start gap-1.5 rounded-md bg-warning/15 px-2.5 py-1.5 text-xs text-warning-foreground">
          <Flame className="mt-0.5 size-3.5 shrink-0" />
          {densityNote}
        </p>
      ) : null}
    </div>
  );
}

export function GradingResultPanel({ submission }: { submission: SubmissionDetail }) {
  const summary = submission.overallSummary;
  return (
    <div className="space-y-6">
      {summary ? (
        <Card className="border-border shadow-none">
          <CardContent className="flex flex-wrap items-center gap-x-6 gap-y-2 py-4">
            <div>
              <p className="text-xs text-muted-foreground">总体预估通过率</p>
              <p className="text-numeric text-2xl font-semibold">
                {Math.round(
                  (summary.overallPassProbability > 1
                    ? summary.overallPassProbability / 100
                    : summary.overallPassProbability) * 100,
                )}
                %
              </p>
            </div>
            <Badge
              variant="outline"
              className={cn(
                "border-transparent",
                summary.overallPassBool
                  ? "bg-success/12 text-success"
                  : "bg-destructive/12 text-destructive",
              )}
            >
              {summary.overallPassBool ? "整体达标" : "整体未达标"}
            </Badge>
            <span className="text-xs text-muted-foreground">
              需全部维度达标方为通过（估算值，非官方）
            </span>
            {summary.cumulativeDensityNote ? (
              <p className="inline-flex w-full items-start gap-1.5 rounded-md bg-warning/15 px-2.5 py-1.5 text-xs text-warning-foreground">
                <Flame className="mt-0.5 size-3.5 shrink-0" />
                {summary.cumulativeDensityNote}
              </p>
            ) : null}
          </CardContent>
        </Card>
      ) : null}
      <Card className="border-border shadow-none">
        <CardHeader>
          <CardTitle className="text-base">
            错误清单
            <span className="text-numeric ml-2 text-sm font-normal text-muted-foreground">
              共 {submission.errorList.length} 条
            </span>
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3 pt-0">
          {submission.errorList.map((e) => (
            <div key={e.id} className="rounded-lg border border-border p-4">
              <div className="mb-2 flex flex-wrap items-center gap-2">
                <Badge variant="outline" className="border-accent/40 text-accent">
                  {e.errorCategory}
                </Badge>
                <Badge variant="outline" className="border-border text-muted-foreground">
                  {e.dimensionKey}
                </Badge>
                {e.impactsCore ? (
                  <span className="inline-flex items-center gap-1 text-xs text-destructive">
                    <AlertCircle className="size-3.5" />
                    影响核心意义点
                  </span>
                ) : (
                  <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
                    <CheckCircle2 className="size-3.5" />
                    非核心
                  </span>
                )}
              </div>
              {e.sourceTextSnippet ? (
                <p className="mb-1 text-sm text-muted-foreground">原文：{e.sourceTextSnippet}</p>
              ) : null}
              {e.userTextSnippet ? (
                <p className="mb-2 text-sm">你的译文：{e.userTextSnippet}</p>
              ) : null}
              {e.explanation ? <p className="text-sm leading-relaxed">{e.explanation}</p> : null}
              {e.suggestion ? (
                <p className="mt-1 text-sm text-primary">建议：{e.suggestion}</p>
              ) : null}
            </div>
          ))}
        </CardContent>
      </Card>

      <Card className="border-border shadow-none">
        <CardHeader>
          <CardTitle className="text-base">维度评分</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          {submission.gradingResults.map((r) => (
            <DimensionBandRow
              key={r.id}
              name={r.dimensionName}
              band={r.band}
              pass={r.passBool}
              rationale={r.rationale}
              densityNote={r.cumulativeDensityNote}
              probability={r.estimatedPassProbability}
            />
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
