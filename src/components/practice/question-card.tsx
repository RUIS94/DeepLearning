import Link from "next/link";
import { ArrowRight, FileText } from "lucide-react";
import type { QuestionListItem } from "@/lib/types/dtos";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { DifficultyBadge, TaskTypeBadge } from "./difficulty-badge";
import { formatDate } from "@/lib/band";

export function QuestionCard({ question }: { question: QuestionListItem }) {
  return (
    <Card className="group border-border shadow-none transition-shadow hover:shadow-[var(--shadow-paper)]">
      <CardContent className="flex flex-col gap-4 p-5">
        <div className="flex flex-wrap items-center gap-2">
          <TaskTypeBadge taskType={question.taskType} />
          <DifficultyBadge difficulty={question.difficulty} />
          {question.inBank ? (
            <Badge variant="outline" className="border-primary/30 text-primary">
              题库
            </Badge>
          ) : null}
        </div>
        <h3 className="text-base font-semibold leading-snug">{question.title}</h3>
        <div className="flex items-center justify-between text-xs text-muted-foreground">
          <span className="text-numeric inline-flex items-center gap-1">
            <FileText className="size-3.5" />
            {question.wordCount ?? "—"} 词 · {formatDate(question.createdAt)}
          </span>
          <Link
            href={`/practice/${question.id}`}
            className="inline-flex items-center gap-1 font-medium text-primary"
          >
            开始作答
            <ArrowRight className="size-3.5 transition-transform group-hover:translate-x-0.5" />
          </Link>
        </div>
      </CardContent>
    </Card>
  );
}
