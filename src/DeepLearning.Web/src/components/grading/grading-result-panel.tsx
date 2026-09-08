"use client";

import { useQuery } from "@tanstack/react-query";
import { AlertCircle, CheckCircle2, Flame, MessageSquare, Scale } from "lucide-react";
import type { SubmissionDetail } from "@/lib/types/dtos";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { bandToColor } from "@/lib/band";
import {
  ErrorSeverity,
  FollowUpThreadKind,
  FollowUpThreadStatus,
  SubmissionStatus,
} from "@/lib/types/enums";
import { useT } from "@/lib/i18n";
import { useBandLabel, useEnumLabels, useErrorImpactLabel } from "@/lib/i18n/enum-labels";
import { cn } from "@/lib/utils";
import { FollowUpPanel } from "@/components/grading/follow-up-panel";
import { listFollowUpThreads } from "@/lib/api/follow-up-threads";
import { listAssessmentDimensions } from "@/lib/api/exam-config";
import { useExamType } from "@/hooks/use-exam-config";

const SEVERITY_TEXT: Record<number, string> = {
  [ErrorSeverity.minor]: "text-muted-foreground",
  [ErrorSeverity.major]: "text-destructive",
};

/** code → 展示名：下划线转空格、首字母大写。用于没有独立 name 字段的分类 code。 */
function humanizeCode(code: string): string {
  return code.replace(/_/g, " ").replace(/^./, (c) => c.toUpperCase());
}

/** passThreshold 是自由文本（"2" / "Band 2" / "Band 2 or above"）——取其中第一个整数当作通过线 Band。 */
function parsePassBand(threshold: string | null | undefined): number | null {
  if (!threshold) return null;
  const m = threshold.match(/\d+/);
  return m ? Number(m[0]) : null;
}

function DimensionBandRow({
  name,
  passBand,
  band,
  pass,
  rationale,
  densityNote,
  probability,
  confidence,
  alternativeBand,
}: {
  name: string;
  passBand: number | null;
  band: number;
  pass: boolean;
  rationale: string;
  densityNote: string | null;
  probability: number | null;
  confidence: "high" | "medium" | "low" | null;
  alternativeBand: number | null;
}) {
  const t = useT();
  const bandLabel = useBandLabel();
  const confidenceLabel: Record<string, string> = {
    high: t("grading.confidence.high"),
    medium: t("grading.confidence.medium"),
    low: t("grading.confidence.low"),
  };
  // 只有在评卷阶段确实给出了另一个候选档时才提示：alternativeBand === band 表示"没有第二选择"。
  const contested = alternativeBand !== null && alternativeBand !== band;
  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-center gap-3">
        <span
          className="text-numeric flex size-9 items-center justify-center rounded-md text-sm font-semibold text-primary-foreground"
          style={{ backgroundColor: bandToColor(band) }}
        >
          {band}
        </span>
        <div className="flex-1">
          <p className="text-sm font-medium">
            {name}
            {passBand !== null ? (
              <span className="ml-2 text-xs font-normal text-muted-foreground">
                {t("grading.passLineBand", { band: passBand })}
              </span>
            ) : null}
          </p>
          <p className="text-xs text-muted-foreground">
            Band {band} · {bandLabel(band)}
            {probability !== null
              ? ` · ${t("grading.estPassProbability", {
                  pct: Math.round((probability > 1 ? probability / 100 : probability) * 100),
                })}`
              : ""}
            {confidence ? ` · ${confidenceLabel[confidence]}` : ""}
            {contested ? t("grading.alternativeBand", { band: alternativeBand ?? "" }) : ""}
          </p>
        </div>
        <Badge
          variant="outline"
          className={cn(
            "border-transparent",
            pass ? "bg-success/12 text-success" : "bg-destructive/12 text-destructive",
          )}
        >
          {pass ? t("grading.pass") : t("grading.fail")}
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
  const t = useT();
  const { ErrorSeverityLabel } = useEnumLabels();
  const errorImpactLabel = useErrorImpactLabel();
  const summary = submission.overallSummary;
  // errorList 只有 dimensionKey，没有维度名；从评分结果里按 key 取对应的展示名。
  const dimensionNameByKey = new Map(
    submission.gradingResults.map((r) => [r.dimensionKey, r.dimensionName]),
  );

  // 官方通过线 Band —— SubmissionDetail 不带，按当前 examType 拉一次 assessment-dimensions，按 key 映射。
  const examType = useExamType();
  const dimensions = useQuery({
    queryKey: ["assessment-dimensions", examType.data?.id],
    queryFn: () => listAssessmentDimensions(examType.data!.id),
    enabled: !!examType.data?.id,
  });
  const passBandByKey = new Map(
    (dimensions.data ?? []).map((d) => [d.dimensionKey, parsePassBand(d.passThreshold)]),
  );
  // 结果区在这些状态下都在（见 submission-page 的 graded 判断），改判入口的可见性再据线程情况细分。
  const resultsVisible =
    submission.status === SubmissionStatus.graded ||
    submission.status === SubmissionStatus.regraded ||
    submission.status === SubmissionStatus.standard_revised ||
    submission.status === SubmissionStatus.under_dispute;

  const threads = useQuery({
    queryKey: ["follow-up-threads", submission.id],
    queryFn: () => listFollowUpThreads(submission.id),
    enabled: resultsVisible,
  });
  const openThread = threads.data?.find((th) => th.status === FollowUpThreadStatus.open) ?? null;
  const openScoreChallenge =
    openThread?.kind === FollowUpThreadKind.score_challenge ? openThread : null;
  const noOpenThread = threads.isSuccess && !openThread;
  const canStartChallenge =
    noOpenThread &&
    (submission.status === SubmissionStatus.graded ||
      submission.status === SubmissionStatus.regraded);

  // 某个维度行是否给出改判入口：没有进行中线程时给"申请改判"；
  // 已有针对该维度的进行中改判申请时给"查看进行中的改判申请"（这也是关掉 popup 后再打开的入口）。
  const challengeFor = (dimensionId: string): "start" | "reopen" | null => {
    if (openScoreChallenge) {
      return openScoreChallenge.dimensionId === dimensionId ? "reopen" : null;
    }
    return canStartChallenge ? "start" : null;
  };

  return (
    <div className="space-y-6">
      {summary ? (
        <Card className="border-border shadow-none">
          <CardContent className="flex flex-wrap items-center gap-x-6 gap-y-2 py-4">
            <div>
              <p className="text-xs text-muted-foreground">{t("grading.overallPassProbability")}</p>
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
              {summary.overallPassBool ? t("grading.overallPass") : t("grading.overallFail")}
            </Badge>
            <span className="text-xs text-muted-foreground">{t("grading.allDimsNote")}</span>
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
            {t("grading.errorList")}
            <span className="text-numeric ml-2 text-sm font-normal text-muted-foreground">
              {t("grading.errorCount", { count: submission.errorList.length })}
            </span>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          {submission.errorList.map((e, i) => (
            <div
              key={e.id}
              className="border-b border-dashed border-border py-4 first:pt-0 last:border-0 last:pb-0"
            >
              <div className="mb-2 flex items-start justify-between gap-3">
                <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                  <span
                    className={cn(
                      "text-xs",
                      SEVERITY_TEXT[e.severity] ?? SEVERITY_TEXT[ErrorSeverity.minor],
                    )}
                  >
                    {e.summary
                      ? t("grading.severityWithSummary", {
                          label: ErrorSeverityLabel[e.severity] ?? e.severity,
                          summary: e.summary,
                        })
                      : (ErrorSeverityLabel[e.severity] ?? e.severity)}
                  </span>
                  {(() => {
                    const impact = errorImpactLabel(e.severity);
                    const Icon = impact.tone === "danger" ? AlertCircle : CheckCircle2;
                    return (
                      <span
                        className={cn(
                          "inline-flex items-center gap-1 text-xs",
                          impact.tone === "danger" ? "text-destructive" : "text-muted-foreground",
                        )}
                      >
                        <Icon className="size-3.5" />
                        {impact.text}
                      </span>
                    );
                  })()}
                </div>
                <div className="shrink-0 text-right text-xs text-muted-foreground">
                  {humanizeCode(e.errorCategory)}
                  {" · "}
                  {dimensionNameByKey.get(e.dimensionKey) ?? humanizeCode(e.dimensionKey)}
                </div>
              </div>
              {e.sourceTextSnippet ? (
                <p className="mb-1 text-sm text-muted-foreground">
                  {t("grading.sourceLabel", { text: e.sourceTextSnippet })}
                </p>
              ) : null}
              {e.userTextSnippet ? (
                <p className="mb-2 text-sm">
                  {t("grading.yourTranslationLabel", { text: e.userTextSnippet })}
                </p>
              ) : null}
              {e.explanation ? <p className="text-sm leading-relaxed">{e.explanation}</p> : null}
              {e.suggestion ? (
                <p className="mt-1 text-sm text-primary">
                  {t("grading.suggestionLabel", { text: e.suggestion })}
                </p>
              ) : null}
              {/* 始终挂载：popup 打开后即使 canStartChallenge 变 false（发送首条消息建线程后）也不卸载。 */}
              <div className={cn("flex justify-end", canStartChallenge && "mt-2")}>
                <FollowUpPanel
                  submissionId={submission.id}
                  disputeAnchor={{
                    contextRef: t("grading.errorAnchor", {
                      index: i + 1,
                      ref: e.positionRef ?? "?",
                      category: e.errorCategory,
                    }).slice(0, 100),
                    label: `${e.dimensionKey} / ${e.errorCategory}${e.summary ? " · " + e.summary : ""}`,
                  }}
                  renderTrigger={(openPanel) =>
                    canStartChallenge ? (
                      <button
                        type="button"
                        onClick={openPanel}
                        className="inline-flex cursor-pointer items-center gap-1 rounded-md px-2 py-1 text-xs text-secondary hover:bg-secondary/10"
                      >
                        <MessageSquare className="size-3" />
                        {t("grading.askAboutError")}
                      </button>
                    ) : null
                  }
                />
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card className="border-border shadow-none">
        <CardHeader>
          <CardTitle className="text-base">{t("grading.dimensionScores")}</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          {submission.gradingResults.map((r) => {
            const mode = challengeFor(r.dimensionId);
            return (
              <div
                key={r.id}
                className="border-b border-dashed border-border py-4 first:pt-0 last:border-0 last:pb-0"
              >
                <DimensionBandRow
                  name={r.dimensionName}
                  passBand={passBandByKey.get(r.dimensionKey) ?? null}
                  band={r.band}
                  pass={r.passBool}
                  rationale={r.rationale}
                  densityNote={r.cumulativeDensityNote}
                  probability={r.estimatedPassProbability}
                  confidence={r.confidence}
                  alternativeBand={r.alternativeBand}
                />
                {/* 改判入口：每个维度右下角，虚线之上。始终挂载 FollowUpPanel，是否给出可点入口由 renderTrigger 决定。 */}
                <div className={cn("flex justify-end", mode && "mt-2")}>
                  <FollowUpPanel
                    submissionId={submission.id}
                    scoreChallenge={{ dimensionId: r.dimensionId, dimensionKey: r.dimensionKey }}
                    renderTrigger={(openPanel) =>
                      mode ? (
                        <button
                          type="button"
                          onClick={openPanel}
                          className="inline-flex items-center gap-1 text-xs cursor-pointer text-secondary hover:text-secondary/80"
                        >
                          {mode === "reopen" ? (
                            <MessageSquare className="size-3" />
                          ) : (
                            <Scale className="size-3" />
                          )}
                          {mode === "reopen"
                            ? t("grading.viewOngoingChallenge")
                            : t("grading.challengeThisDimension")}
                        </button>
                      ) : null
                    }
                  />
                </div>
              </div>
            );
          })}
        </CardContent>
      </Card>
    </div>
  );
}
