"use client";

import Link from "next/link";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/shared/empty-state";
import { useReviewLibrary } from "@/components/review/use-review-library";
import { useCurrentUser } from "@/hooks/use-current-user";
import { MasteryLevel } from "@/lib/types/enums";
import { useT } from "@/lib/i18n";
import { useEnumLabels } from "@/lib/i18n/enum-labels";
import { formatDate } from "@/lib/band";

const ALL = "all";

type ReviewRow = {
  id: string;
  title: string;
  subtitle: string | null;
  subtitleClassName: string;
  /** vocab 专用:AI 维护的跨题累积语义。 */
  body: string | null;
  domain: string | null;
  scenario: string | null;
  frequencyTag: string | null;
  /** vocab 专用:词条附加计数标签(义项数 / 出现次数)。 */
  countNote: string | null;
  timesEncountered: number;
  lastReviewedAt: string | null;
  questionId: string | null;
  masteryLevel: number;
};

function MasteryPicker({
  value,
  onChange,
  pending,
}: {
  value: number;
  onChange: (level: number) => void;
  pending: boolean;
}) {
  const { MasteryLevelLabel } = useEnumLabels();
  return (
    <div className="flex gap-1">
      {Object.values(MasteryLevel).map((level) => (
        <Button
          key={level}
          size="sm"
          variant={value === level ? "default" : "outline"}
          disabled={pending}
          onClick={() => onChange(level)}
        >
          {MasteryLevelLabel[level]}
        </Button>
      ))}
    </div>
  );
}

export function ReviewLibraryList({
  kind,
  mastery,
  domain,
}: {
  kind: "patterns" | "vocab";
  mastery: string;
  domain: string;
}) {
  const t = useT();
  const currentUser = useCurrentUser();
  const { patterns, vocab, markPattern, markVocab } = useReviewLibrary(currentUser.data?.id);

  const query = kind === "patterns" ? patterns : vocab;
  const markPending = kind === "patterns" ? markPattern.isPending : markVocab.isPending;
  const mark = (id: string, level: number) => {
    if (kind === "patterns") markPattern.mutate({ id, level });
    else markVocab.mutate({ id, level });
  };

  const rows: ReviewRow[] =
    kind === "patterns"
      ? (patterns.data ?? []).map((p) => ({
          id: p.id,
          title: p.patternName,
          subtitle: p.exampleSentence,
          subtitleClassName: "source-text text-sm text-muted-foreground",
          body: null,
          domain: p.domain,
          scenario: p.scenario,
          frequencyTag: p.frequencyTag,
          countNote: null,
          timesEncountered: p.timesEncountered,
          lastReviewedAt: p.lastReviewedAt,
          questionId: p.questionId,
          masteryLevel: p.masteryLevel,
        }))
      : (vocab.data ?? []).map((v) => ({
          id: v.id,
          title: v.englishExpr,
          subtitle: v.chineseEquiv,
          subtitleClassName: "text-sm text-primary",
          body: v.accumulatedSemantics || null,
          domain: v.domain,
          scenario: v.scenario,
          frequencyTag: v.category ?? v.frequencyTag,
          countNote:
            v.occurrenceCount > 1
              ? t("review.lib.countNote", {
                  senses: v.senseCount,
                  occurrences: v.occurrenceCount,
                })
              : null,
          timesEncountered: v.timesEncountered,
          lastReviewedAt: v.lastReviewedAt,
          questionId: null,
          masteryLevel: v.masteryLevel,
        }));

  const filtered = rows
    .filter((r) => (mastery === ALL ? true : r.masteryLevel === Number(mastery)))
    .filter((r) => (domain === ALL ? true : r.domain === domain));

  if (query.isPending) {
    return <Skeleton className="h-40 w-full rounded-xl" />;
  }

  if (filtered.length === 0) {
    const label = kind === "patterns" ? t("review.lib.kindPatterns") : t("review.lib.kindVocab");
    return (
      <EmptyState>
        {rows.length === 0
          ? t("review.lib.emptyNone", { kind: label })
          : t("review.lib.emptyFiltered", { kind: label })}
      </EmptyState>
    );
  }

  return (
    <div className="space-y-4">
      {filtered.map((r) => (
        <Card key={r.id} className="border-border shadow-none">
          <CardContent className="flex flex-wrap items-start justify-between gap-4 p-5">
            <div className="min-w-64 flex-1 space-y-2">
              <p className="text-sm font-medium">{r.title}</p>
              <p className={r.subtitleClassName}>{r.subtitle}</p>
              {r.body ? (
                <p className="whitespace-pre-line text-sm text-muted-foreground">{r.body}</p>
              ) : null}
              <div className="flex flex-wrap items-center gap-2">
                {[r.domain, r.scenario, r.frequencyTag].filter(Boolean).map((tag) => (
                  <Badge
                    key={tag}
                    variant="outline"
                    className="border-border text-muted-foreground"
                  >
                    {tag}
                  </Badge>
                ))}
                <span className="text-numeric text-xs text-muted-foreground">
                  {r.countNote ? `${r.countNote} · ` : ""}
                  {t("review.lib.encounteredTimes", { count: r.timesEncountered })} ·{" "}
                  {t("review.lib.lastReviewedPrefix")} {formatDate(r.lastReviewedAt)}
                </span>
                {r.questionId ? (
                  <Link
                    href={`/practice/${r.questionId}`}
                    className="text-xs text-primary underline underline-offset-2"
                  >
                    {t("review.lib.backToQuestion")}
                  </Link>
                ) : null}
              </div>
            </div>
            <MasteryPicker
              value={r.masteryLevel}
              pending={markPending}
              onChange={(level) => mark(r.id, level)}
            />
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
