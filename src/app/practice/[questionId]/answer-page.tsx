"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Highlighter, Send, Trash2 } from "lucide-react";
import { AppShell } from "@/components/shared/app-shell";
import { ErrorBanner } from "@/components/shared/ai-loading-state";
import { SelectableSourceText } from "@/components/practice/selectable-source-text";
import { DifficultyBadge, TaskTypeBadge } from "@/components/practice/difficulty-badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Textarea } from "@/components/ui/textarea";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  createSubmission,
  errorTaxonomies,
  getQuestionById,
  listSeedReferences,
  MOCK_USER,
} from "@/lib/mock/store";
import { QuestionOrigin, TaskType } from "@/lib/types/enums";
import type { TaskBAnnotation } from "@/lib/types/dtos";
import { taskAContentSchema, taskBContentSchema } from "@/lib/validation/submission";

export function AnswerPage() {
  const { questionId } = useParams<{ questionId: string }>();
  const router = useRouter();
  const question = useQuery({
    queryKey: ["question", questionId],
    queryFn: () => getQuestionById(questionId),
  });
  // design doc §11.2 Step 8 的真题溯源：AI 出题时参考了哪些真题种子，只对 ai_generated 题目有意义。
  const seedReferences = useQuery({
    queryKey: ["seed-references", questionId],
    queryFn: () => listSeedReferences(questionId),
    enabled: question.data?.origin === QuestionOrigin.ai_generated,
  });

  const [translation, setTranslation] = useState("");
  const [annotations, setAnnotations] = useState<TaskBAnnotation[]>([]);
  const [draft, setDraft] = useState<{ start: number; end: number } | null>(null);
  const [draftCategory, setDraftCategory] = useState(errorTaxonomies[0]!.categoryKey);
  const [draftCorrected, setDraftCorrected] = useState("");

  const submit = useMutation({
    mutationFn: () => {
      const isTaskB = question.data?.taskType === TaskType.B;
      return createSubmission({
        questionId,
        userId: MOCK_USER.id,
        taskType: question.data?.taskType ?? TaskType.A,
        content: JSON.stringify(isTaskB ? annotations : translation),
      });
    },
    onSuccess: (submission) => router.push(`/submissions/${submission.id}`),
  });

  if (question.isPending) {
    return (
      <AppShell title="答题">
        <Skeleton className="h-96 w-full rounded-xl" />
      </AppShell>
    );
  }

  if (question.isError || !question.data) {
    return (
      <AppShell title="答题">
        <ErrorBanner error={question.error} />
      </AppShell>
    );
  }

  const q = question.data;
  const isTaskB = q.taskType === TaskType.B;
  const flawed = q.taskB?.flawedTranslationText ?? "";
  // 提交前的前端校验镜像后端 CreateSubmissionValidator（方案 §11），提前拦截而非等后端 400。
  const contentValidation = isTaskB
    ? taskBContentSchema.safeParse(annotations)
    : taskAContentSchema.safeParse(translation);
  const canSubmit = contentValidation.success;

  return (
    <AppShell
      title={q.title}
      description={q.brief ?? undefined}
      actions={
        <>
          <TaskTypeBadge taskType={q.taskType} />
          <DifficultyBadge difficulty={q.difficulty} />
        </>
      }
    >
      <div className="grid gap-6 lg:grid-cols-2">
        <div className="space-y-6">
          <Card className="border-border shadow-none">
            <CardHeader>
              <CardTitle className="text-base">原文</CardTitle>
            </CardHeader>
            <CardContent>
              <p className="source-text text-[15px]">{q.sourceText}</p>
              {q.meaningCheckpoints.length ? (
                <div className="mt-6 space-y-2 border-t border-border pt-4">
                  <p className="text-xs font-medium text-muted-foreground">核心意义点</p>
                  <ul className="space-y-1">
                    {q.meaningCheckpoints.map((c) => (
                      <li key={c.id} className="flex items-start gap-2 text-sm">
                        <span
                          className={`mt-1.5 size-1.5 shrink-0 rounded-full ${
                            c.importance === 0 ? "bg-accent" : "bg-muted-foreground"
                          }`}
                        />
                        {c.checkpointText}
                      </li>
                    ))}
                  </ul>
                </div>
              ) : null}
            </CardContent>
          </Card>

          {seedReferences.data?.length ? (
            <Card className="border-border shadow-none">
              <CardHeader>
                <CardTitle className="text-base">参考真题</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2">
                {seedReferences.data.map((r) => (
                  <div key={r.id} className="rounded-md border border-border p-3 text-sm">
                    <Link
                      href={`/practice/${r.seedQuestionId}`}
                      className="text-primary underline underline-offset-2"
                    >
                      {r.seedQuestionTitle}
                    </Link>
                    {r.similarityReason ? (
                      <p className="mt-1 text-xs text-muted-foreground">{r.similarityReason}</p>
                    ) : null}
                  </div>
                ))}
              </CardContent>
            </Card>
          ) : null}
        </div>

        <div className="space-y-6">
          {isTaskB ? (
            <Card className="border-border shadow-none">
              <CardHeader>
                <CardTitle className="flex items-center gap-2 text-base">
                  <Highlighter className="size-4 text-accent" />
                  待检译文（拖选文字进行标注）
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <SelectableSourceText
                  text={flawed}
                  highlightRanges={annotations.map((a) => ({
                    positionStart: a.positionStart,
                    positionEnd: a.positionEnd,
                    tone: "flag" as const,
                  }))}
                  onSelectRange={(start, end) => {
                    setDraft({ start, end });
                    setDraftCorrected(flawed.slice(start, end));
                  }}
                />

                {draft ? (
                  <div className="space-y-3 rounded-lg border border-primary/40 bg-primary/5 p-4">
                    <p className="text-numeric text-xs text-muted-foreground">
                      选区 [{draft.start}, {draft.end}) ·「{flawed.slice(draft.start, draft.end)}」
                    </p>
                    <div className="space-y-2">
                      <Label>错误类型</Label>
                      <Select value={draftCategory} onValueChange={setDraftCategory}>
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {errorTaxonomies.map((t) => (
                            <SelectItem key={t.id} value={t.categoryKey}>
                              {t.categoryName}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-2">
                      <Label>修正后的文本</Label>
                      <Input
                        value={draftCorrected}
                        onChange={(e) => setDraftCorrected(e.target.value)}
                      />
                    </div>
                    <div className="flex gap-2">
                      <Button
                        size="sm"
                        onClick={() => {
                          setAnnotations((prev) => [
                            ...prev,
                            {
                              positionStart: draft.start,
                              positionEnd: draft.end,
                              errorCategory: draftCategory,
                              correctedText: draftCorrected,
                            },
                          ]);
                          setDraft(null);
                        }}
                      >
                        添加标注
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => setDraft(null)}>
                        取消
                      </Button>
                    </div>
                  </div>
                ) : null}

                <div className="space-y-2">
                  <p className="text-numeric text-xs font-medium text-muted-foreground">
                    已标注 {annotations.length} 处
                  </p>
                  {annotations.map((a, i) => (
                    <div
                      key={`${a.positionStart}-${a.positionEnd}-${i}`}
                      className="flex items-start justify-between gap-3 rounded-md border border-border p-3"
                    >
                      <div className="space-y-1">
                        <div className="flex items-center gap-2">
                          <Badge variant="outline" className="border-accent/40 text-accent">
                            {errorTaxonomies.find((t) => t.categoryKey === a.errorCategory)
                              ?.categoryName ?? a.errorCategory}
                          </Badge>
                          <span className="text-numeric text-xs text-muted-foreground">
                            [{a.positionStart}, {a.positionEnd})
                          </span>
                        </div>
                        <p className="text-sm">
                          <span className="line-through opacity-60">
                            {flawed.slice(a.positionStart, a.positionEnd)}
                          </span>
                          <span className="mx-1">→</span>
                          <span className="text-primary">{a.correctedText}</span>
                        </p>
                      </div>
                      <Button
                        size="icon"
                        variant="ghost"
                        onClick={() => setAnnotations((prev) => prev.filter((_, idx) => idx !== i))}
                      >
                        <Trash2 className="size-4" />
                      </Button>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          ) : (
            <Card className="border-border shadow-none">
              <CardHeader>
                <CardTitle className="text-base">你的译文</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <Textarea
                  rows={16}
                  value={translation}
                  onChange={(e) => setTranslation(e.target.value)}
                  placeholder="在此输入中文译文…"
                  className="source-text"
                />
                <p className="text-numeric text-xs text-muted-foreground">
                  已输入 {translation.length} 字
                </p>
              </CardContent>
            </Card>
          )}

          <div className="space-y-3">
            <Button
              className="w-full"
              disabled={!canSubmit || submit.isPending}
              onClick={() => submit.mutate()}
            >
              <Send className="size-4" />
              {submit.isPending ? "提交中…" : "提交并进入批改"}
            </Button>
            {submit.isError ? <ErrorBanner error={submit.error} /> : null}
          </div>
        </div>
      </div>
    </AppShell>
  );
}
