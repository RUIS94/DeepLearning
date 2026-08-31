"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { BookOpenCheck, Gavel } from "lucide-react";
import { AppShell } from "@/components/shared/app-shell";
import { AiLoadingState, ErrorBanner } from "@/components/shared/ai-loading-state";
import { GradingResultPanel } from "@/components/grading/grading-result-panel";
import { FollowUpDialog } from "@/components/grading/follow-up-dialog";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  getQuestionById,
  getSubmissionById,
  gradeSubmission,
  listFollowUps,
} from "@/lib/mock/store";
import {
  SubmissionStatus,
  SubmissionStatusLabel,
  TaskType,
  FollowUpVerdictLabel,
} from "@/lib/types/enums";
import type { TaskBAnnotation } from "@/lib/types/dtos";

export function SubmissionPage() {
  const { submissionId } = useParams<{ submissionId: string }>();
  const queryClient = useQueryClient();

  const submission = useQuery({
    queryKey: ["submission", submissionId],
    queryFn: () => getSubmissionById(submissionId),
  });
  const question = useQuery({
    queryKey: ["question", submission.data?.questionId],
    queryFn: () => getQuestionById(submission.data!.questionId),
    enabled: !!submission.data,
  });
  const followUps = useQuery({
    queryKey: ["follow-ups", submissionId],
    queryFn: () => listFollowUps(submissionId),
  });

  const grade = useMutation({
    mutationFn: () => gradeSubmission(submissionId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["submission", submissionId] }),
  });

  if (submission.isPending) {
    return (
      <AppShell title="批改结果">
        <Skeleton className="h-96 w-full rounded-xl" />
      </AppShell>
    );
  }
  if (submission.isError || !submission.data) {
    return (
      <AppShell title="批改结果">
        <ErrorBanner error={submission.error} />
      </AppShell>
    );
  }

  const s = submission.data;
  const graded =
    s.status === SubmissionStatus.graded || s.status === SubmissionStatus.standard_revised;
  const archived = s.status === SubmissionStatus.archived;

  let parsedContent: string | TaskBAnnotation[] = "";
  try {
    parsedContent = JSON.parse(s.content);
  } catch {
    parsedContent = s.content;
  }

  return (
    <AppShell
      title={question.data?.title ?? "批改结果"}
      description="AI 依据当前生效的评分标准逐维度给出 Band 与理由。"
      actions={
        <>
          <Badge variant="outline" className="border-primary/30 text-primary">
            {SubmissionStatusLabel[s.status]}
          </Badge>
          {question.data ? (
            <Button variant="outline" asChild>
              <Link href={`/deep-learning/${question.data.id}`}>
                <BookOpenCheck className="size-4" />
                深入学习
              </Link>
            </Button>
          ) : null}
        </>
      }
    >
      <div className="grid gap-6 lg:grid-cols-[1fr_380px]">
        <div className="space-y-6">
          {!graded ? (
            <Card className="border-border shadow-none">
              <CardHeader>
                <CardTitle className="text-base">
                  {s.status === SubmissionStatus.grading_failed ? "批改失败" : "尚未批改"}
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <p className="text-sm text-muted-foreground">
                  {s.status === SubmissionStatus.grading_failed
                    ? "上一次批改未完成，可以重新发起。"
                    : "提交已记录，点击下方按钮开始 AI 批改。"}
                </p>
                <Button disabled={grade.isPending} onClick={() => grade.mutate()}>
                  <Gavel className="size-4" />
                  {grade.isPending
                    ? "批改中…"
                    : s.status === SubmissionStatus.grading_failed
                      ? "重新批改"
                      : "开始批改"}
                </Button>
                <AiLoadingState
                  status={grade.status}
                  error={grade.error}
                  pendingHint="AI 正在批改，可能需要几秒到十几秒"
                />
              </CardContent>
            </Card>
          ) : (
            <GradingResultPanel submission={s} />
          )}

          {s.status === SubmissionStatus.standard_revised ? (
            <div className="rounded-lg border border-success/40 bg-success/8 p-4 text-sm">
              该判定已确认修正，相关评分标准已更新。可在
              <Link
                href="/standard-overrides"
                className="mx-1 text-primary underline underline-offset-2"
              >
                标准修正记录
              </Link>
              中查看追溯。
            </div>
          ) : null}
        </div>

        <div className="space-y-6">
          <Card className="border-border shadow-none">
            <CardHeader>
              <CardTitle className="text-base">你的作答</CardTitle>
            </CardHeader>
            <CardContent>
              {s.taskType === TaskType.B && Array.isArray(parsedContent) ? (
                <ul className="space-y-3">
                  {parsedContent.map((a, i) => (
                    <li key={i} className="rounded-md border border-border p-3 text-sm">
                      <span className="text-numeric text-xs text-muted-foreground">
                        [{a.positionStart}, {a.positionEnd}) · {a.errorCategory}
                      </span>
                      <p className="mt-1 text-primary">{a.correctedText}</p>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="source-text whitespace-pre-wrap text-sm">{String(parsedContent)}</p>
              )}
            </CardContent>
          </Card>

          {graded && !archived ? (
            <div className="space-y-3">
              <FollowUpDialog
                submissionId={submissionId}
                onResolved={() => {
                  queryClient.invalidateQueries({ queryKey: ["submission", submissionId] });
                  queryClient.invalidateQueries({ queryKey: ["follow-ups", submissionId] });
                }}
              />
              {followUps.data?.length ? (
                <Card className="border-border shadow-none">
                  <CardHeader>
                    <CardTitle className="text-base">追问记录</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    {followUps.data.map((f) => (
                      <div key={f.id} className="rounded-md border border-border p-3">
                        <Badge variant="outline" className="border-border text-muted-foreground">
                          {FollowUpVerdictLabel[f.verdict]}
                        </Badge>
                        <p className="mt-2 text-sm leading-relaxed">{f.aiResponse}</p>
                      </div>
                    ))}
                  </CardContent>
                </Card>
              ) : null}
            </div>
          ) : null}
        </div>
      </div>
    </AppShell>
  );
}
