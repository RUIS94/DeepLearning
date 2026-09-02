"use client";

import Link from "next/link";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { useReviewLibrary } from "@/components/review/use-review-library";
import { useCurrentUser } from "@/hooks/use-current-user";
import { MasteryLevel, MasteryLevelLabel } from "@/lib/types/enums";
import { formatDate } from "@/lib/band";

const ALL = "all";

type ReviewRow = {
  id: string;
  title: string;
  subtitle: string | null;
  subtitleClassName: string;
  domain: string | null;
  scenario: string | null;
  frequencyTag: string | null;
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
          domain: p.domain,
          scenario: p.scenario,
          frequencyTag: p.frequencyTag,
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
          domain: v.domain,
          scenario: v.scenario,
          frequencyTag: v.frequencyTag,
          timesEncountered: v.timesEncountered,
          lastReviewedAt: v.lastReviewedAt,
          questionId: v.questionId,
          masteryLevel: v.masteryLevel,
        }));

  const filtered = rows
    .filter((r) => (mastery === ALL ? true : r.masteryLevel === Number(mastery)))
    .filter((r) => (domain === ALL ? true : r.domain === domain));

  if (query.isPending) {
    return <Skeleton className="h-40 w-full rounded-xl" />;
  }

  return (
    <div className="space-y-4">
      {filtered.map((r) => (
        <Card key={r.id} className="border-border shadow-none">
          <CardContent className="flex flex-wrap items-start justify-between gap-4 p-5">
            <div className="min-w-64 flex-1 space-y-2">
              <p className="text-sm font-medium">{r.title}</p>
              <p className={r.subtitleClassName}>{r.subtitle}</p>
              <div className="flex flex-wrap items-center gap-2">
                {[r.domain, r.scenario, r.frequencyTag].filter(Boolean).map((t) => (
                  <Badge key={t} variant="outline" className="border-border text-muted-foreground">
                    {t}
                  </Badge>
                ))}
                <span className="text-numeric text-xs text-muted-foreground">
                  遇见 {r.timesEncountered} 次 · 上次复习 {formatDate(r.lastReviewedAt)}
                </span>
                {r.questionId ? (
                  <Link
                    href={`/practice/${r.questionId}`}
                    className="text-xs text-primary underline underline-offset-2"
                  >
                    回到原题
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
