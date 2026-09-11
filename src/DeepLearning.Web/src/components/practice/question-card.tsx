"use client";

import Link from "next/link";
import { ArrowRight, FileText, History, Upload } from "lucide-react";
import type { QuestionListItem } from "@/lib/types/dtos";
import { Card, CardContent } from "@/components/ui/card";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { useEnumLabels } from "@/lib/i18n/enum-labels";
import { formatDate } from "@/lib/band";
import { useT } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { difficultyTextClass } from "@/lib/enum-tone";

export function QuestionCard({
  question,
  onOpenRecords,
}: {
  question: QuestionListItem;
  onOpenRecords?: (question: QuestionListItem) => void;
}) {
  const t = useT();
  const { TaskTypeLabel, DifficultyLabel } = useEnumLabels();
  const practiced = question.myAttemptCount > 0;

  return (
    <Card className="group h-full border-border shadow-none transition-shadow hover:shadow-[var(--shadow-paper)]">
      <CardContent className="flex h-full flex-col gap-4 p-5">
        <div className="flex items-center justify-between gap-2 text-xs">
          <div className="flex min-w-0 items-center gap-2">
            <span className="shrink-0 font-semibold text-secondary">
              {TaskTypeLabel[question.taskType]}
            </span>
            <span className="text-numeric inline-flex min-w-0 items-center gap-1 text-muted-foreground">
              <FileText className="size-3.5 shrink-0" />
              <span className="truncate">
                {t("questionCard.words", { count: question.wordCount ?? "—" })} ·{" "}
                {formatDate(question.createdAt)}
              </span>
            </span>
          </div>
          <div className="flex shrink-0 items-center gap-1.5">
            {question.inBank ? (
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <span className="inline-flex items-center text-muted-foreground">
                      <Upload className="size-3.5" />
                    </span>
                  </TooltipTrigger>
                  <TooltipContent>{t("questionCard.imported")}</TooltipContent>
                </Tooltip>
              </TooltipProvider>
            ) : null}
            <span className={cn("font-semibold", difficultyTextClass(question.difficulty))}>
              {DifficultyLabel[question.difficulty]}
            </span>
          </div>
        </div>

        <h3 className="flex-1 text-base font-semibold leading-snug">{question.title}</h3>

        <div className="-my-1 flex items-center justify-between gap-2 text-xs">
          <span className="font-medium text-success">
            {practiced
              ? t("questionCard.practicedTimes", { count: question.myAttemptCount })
              : null}
          </span>
          <div className="-mr-2 flex items-center gap-1">
            {practiced && onOpenRecords ? (
              <button
                type="button"
                onClick={() => onOpenRecords(question)}
                className="inline-flex cursor-pointer items-center gap-1 rounded-md px-2 py-1 font-medium text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
              >
                <History className="size-3.5" />
                {t("questionCard.records")}
              </button>
            ) : null}
            <Link
              href={`/practice/${question.id}`}
              className="inline-flex items-center gap-1 rounded-md px-2 py-1 font-medium text-primary transition-colors hover:bg-primary/10"
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
