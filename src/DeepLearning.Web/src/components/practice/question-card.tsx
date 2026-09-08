"use client";

import Link from "next/link";
import { ArrowRight, FileText, History } from "lucide-react";
import type { QuestionListItem } from "@/lib/types/dtos";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { DifficultyBadge, TaskTypeBadge } from "./difficulty-badge";
import { formatDate } from "@/lib/band";
import { useT } from "@/lib/i18n";

export function QuestionCard({
  question,
  onOpenRecords,
}: {
  question: QuestionListItem;
  onOpenRecords?: (question: QuestionListItem) => void;
}) {
  const t = useT();
  const practiced = question.myAttemptCount > 0;

  return (
    <Card className="group h-full border-border shadow-none transition-shadow hover:shadow-[var(--shadow-paper)]">
      <CardContent className="flex h-full flex-col gap-4 p-5">
        <div className="flex flex-wrap items-center gap-2">
          <TaskTypeBadge taskType={question.taskType} />
          <DifficultyBadge difficulty={question.difficulty} />
          {question.inBank ? (
            <Badge variant="outline" className="border-primary/30 text-primary">
              {t("questionCard.imported")}
            </Badge>
          ) : null}
          {practiced ? (
            <Badge variant="outline" className="border-transparent bg-success/12 text-success">
              {t("questionCard.practicedTimes", { count: question.myAttemptCount })}
            </Badge>
          ) : null}
        </div>
        <h3 className="flex-1 text-base font-semibold leading-snug">{question.title}</h3>
        <div className="flex items-center justify-between text-xs text-muted-foreground">
          <span className="text-numeric inline-flex items-center gap-1">
            <FileText className="size-3.5" />
            {t("questionCard.words", { count: question.wordCount ?? "—" })} ·{" "}
            {formatDate(question.createdAt)}
          </span>
          <div className="flex items-center gap-3">
            {practiced && onOpenRecords ? (
              <button
                type="button"
                onClick={() => onOpenRecords(question)}
                className="inline-flex items-center gap-1 font-medium text-muted-foreground transition-colors hover:text-foreground"
              >
                <History className="size-3.5" />
                {t("questionCard.records")}
              </button>
            ) : null}
            <Link
              href={`/practice/${question.id}`}
              className="inline-flex items-center gap-1 font-medium text-primary"
            >
              {t("questionCard.start")}
              <ArrowRight className="size-3.5 transition-transform group-hover:translate-x-0.5" />
            </Link>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
