"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { BookOpenCheck, Gavel, Loader2, RefreshCw } from "lucide-react";
import { AppShell } from "@/components/shared/app-shell";
import { AiLoadingState, ErrorBanner } from "@/components/shared/ai-loading-state";
import { GradingResultPanel } from "@/components/grading/grading-result-panel";
import { FollowUpPanel } from "@/components/grading/follow-up-panel";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { getQuestionById } from "@/lib/api/questions";
import {
  getSubmissionById,
  gradeSubmission,
  regenerateWeakPoints,
  watchGradingStatus,
} from "@/lib/api/submissions";
import { useExamType } from "@/hooks/use-exam-config";
import {
  SubmissionStatus,
  SubmissionStatusLabel,
  TaskType,
  WeakPointGenerationStatus,
  WeakPointGenerationStatusLabel,
} from "@/lib/types/enums";
import type { TaskBAnnotation } from "@/lib/types/dtos";
import { cn } from "@/lib/utils";

/**
 * 每个监视请求最多挂在服务端多久。到点后原样再发一个——所以稳态下大约一分钟一个请求，
 * 而不是每几秒打一次。状态一变，服务端在两秒内就会让请求返回。
 */
const WATCH_SECONDS = 55;

/** 一次监视请求返回后，隔多久发下一个。只是为了不在返回的同一刻立刻重连。 */
const WATCH_GAP_MS = 1000;

/** 超过这个时长仍停在 grading，就在 loading 里加一句"比平常久"，但继续等，不做任何自动重试。 */
const SLOW_AFTER_MS = 6 * 60 * 1000;

/** 入队后状态迟迟不离开 submitted，说明后台 worker 可能没接到活儿——提示用户，仍不自动重试。 */
const HANDOFF_STUCK_AFTER_MS = 30 * 1000;

/** 薄弱点标签的刷新间隔。它只是个标签，评判结果早就显示出来了，慢一点完全无所谓。 */
const WEAK_POINT_POLL_MS = 30 * 1000;

export function SubmissionPage() {
  const { submissionId } = useParams<{ submissionId: string }>();
  const queryClient = useQueryClient();
  // 何时把任务交给后端的。null = 本次会话没发起过批改。用来区分"刚提交、等着人点批改"和
  // "已经入队、worker 还没把状态翻成 grading"这两种同样是 submitted 的情况。
  const [enqueuedAt, setEnqueuedAt] = useState<number | null>(null);

  const submission = useQuery({
    queryKey: ["submission", submissionId],
    queryFn: () => getSubmissionById(submissionId),
    // 批改期间不定时轮询——由下面的 gradingWatch 长轮询盯着，结束时才回来刷一次。
    // 唯一的例外是薄弱点还在后台生成时：那只是个标签，30 秒翻一次完全够用。
    refetchInterval: (query) => {
      const wp = query.state.data?.weakPointGenerationStatus ?? null;
      return wp === WeakPointGenerationStatus.pending || wp === WeakPointGenerationStatus.running
        ? WEAK_POINT_POLL_MS
        : false;
    },
  });
  const question = useQuery({
    queryKey: ["question", submission.data?.questionId],
    queryFn: () => getQuestionById(submission.data!.questionId),
    enabled: !!submission.data,
  });
  // 是否该盯着这次批改。以 submission 的真实状态为准；submitted 只有在【本次会话确实入过队】
  // 时才算——否则一份刚提交、根本没人发起过批改的译文会一进页面就开始等，按钮还被禁用。
  const watchedStatus = submission.data?.status;
  const watching =
    watchedStatus === SubmissionStatus.grading ||
    (enqueuedAt !== null && watchedStatus === SubmissionStatus.submitted);

  // 薄弱点生成是评判之后另起的后台任务，评判结果不等它。它自己还在跑的时候，慢速刷一下让标签
  // 能从"正在生成"翻到"已生成/失败"——这条轮询不影响评判结果的显示，所以放得很松。
  const weakPointStatus = submission.data?.weakPointGenerationStatus ?? null;
  const weakPointsInProgress =
    weakPointStatus === WeakPointGenerationStatus.pending ||
    weakPointStatus === WeakPointGenerationStatus.running;

  const examType = useExamType();

  // 批改期间的唯一网络活动：一个长轮询请求挂在服务端，最多 55 秒；批改一结束（成功或失败）
  // 它就立刻返回，我们再去刷一次 submission 把结果显示出来。这样既不用高频打后端，也不用
  // 等下一个轮询周期才看到结果。
  const gradingWatch = useQuery({
    queryKey: ["grading-status", submissionId],
    queryFn: () => watchGradingStatus(submissionId, WATCH_SECONDS),
    enabled: watching,
    // 上一个请求返回后隔 1 秒再发下一个；terminal 之后停下，交给 enabled 收尾。
    refetchInterval: (query) => (query.state.data?.terminal ? false : WATCH_GAP_MS),
    refetchOnWindowFocus: false,
    // 前端不做任何重试；网络抖动交给下一次周期重连。
    retry: false,
    gcTime: 0,
    staleTime: 0,
  });

  // 后端给出结论的那一刻主动刷新，而不是等某个周期到点。
  useEffect(() => {
    if (gradingWatch.data?.terminal) {
      queryClient.invalidateQueries({ queryKey: ["submission", submissionId] });
    }
  }, [gradingWatch.data?.terminal, gradingWatch.data?.status, queryClient, submissionId]);

  // 只在生成失败后由用户手动点。后端不自动重试（一次生成 = 一次 LLM 调用），前端同样不自动重试。
  const regenerate = useMutation({
    mutationFn: () => regenerateWeakPoints(submissionId, examType.data!.id),
    retry: false,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["submission", submissionId] }),
  });

  const grade = useMutation({
    // examTypeId 镜像后端 GradeSubmissionRequest 的必填字段——题库目前只有一个 examType，
    // 由方案 §9.1 的"ExamTypeId 全局引导"（useExamType hook）提供。
    mutationFn: () => gradeSubmission(submissionId, examType.data!.id),
    // 前端【绝不重试】。批改的重试策略完全在后端：每个阶段最多重新提示 3 次，最终失败会把
    // submission 置为 grading_failed。前端再发一次 POST 只会凭空多跑一整轮四次 LLM 调用。
    // 重新发起只能由用户在失败后手动点击。
    retry: false,
    // 这个 mutation 只负责"把任务交出去"，成功不等于批改完成。
    onSuccess: () => {
      setEnqueuedAt(Date.now());
      queryClient.invalidateQueries({ queryKey: ["submission", submissionId] });
    },
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
  // under_dispute 也算“已出批改结果”：一条追问线程存续期间 submission 会停在
  // under_dispute（见 follow-up-panel.tsx），此时批改结果区域和追问入口都要照常显示。
  const graded =
    s.status === SubmissionStatus.graded ||
    s.status === SubmissionStatus.standard_revised ||
    s.status === SubmissionStatus.under_dispute;
  const archived = s.status === SubmissionStatus.archived;

  // "批改中"以 submission 的真实状态为准，而不是那个几毫秒就返回的 POST——入队之后 mutation
  // 立刻 settled，但活儿才刚开始。
  const handedOff = enqueuedAt !== null;
  const gradingInFlight = grade.isPending || watching;

  // 每次轮询都会重渲染，所以这两个判断不需要单独的计时器就能随时间更新。
  const elapsedMs = enqueuedAt === null ? 0 : Date.now() - enqueuedAt;
  const takingLong = s.status === SubmissionStatus.grading && elapsedMs > SLOW_AFTER_MS;
  const handoffStuck =
    handedOff && s.status === SubmissionStatus.submitted && elapsedMs > HANDOFF_STUCK_AFTER_MS;

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
          {weakPointStatus !== null ? (
            <Badge
              variant="outline"
              className={cn(
                weakPointStatus === WeakPointGenerationStatus.failed
                  ? "border-destructive/40 text-destructive"
                  : weakPointStatus === WeakPointGenerationStatus.succeeded
                    ? "border-success/40 text-success"
                    : "border-border text-muted-foreground",
              )}
            >
              {weakPointsInProgress ? <Loader2 className="size-3 animate-spin" /> : null}
              {WeakPointGenerationStatusLabel[weakPointStatus]}
              {weakPointStatus === WeakPointGenerationStatus.failed ? (
                <button
                  type="button"
                  className="ml-1.5 inline-flex items-center gap-1 underline underline-offset-2 disabled:opacity-50"
                  disabled={regenerate.isPending || !examType.data}
                  onClick={() => regenerate.mutate()}
                >
                  <RefreshCw className={cn("size-3", regenerate.isPending && "animate-spin")} />
                  重新生成
                </button>
              ) : null}
            </Badge>
          ) : null}
          {question.data ? (
            <Button variant="outline" asChild>
              <Link href={`/deep-learning/${question.data.id}`}>
                <BookOpenCheck className="size-4" />
                深入学习
              </Link>
            </Button>
          ) : null}
          {graded && !archived ? (
            <FollowUpPanel
              submissionId={submissionId}
              onChanged={() =>
                queryClient.invalidateQueries({ queryKey: ["submission", submissionId] })
              }
            />
          ) : null}
        </>
      }
    >
      {/* 高度链见 AGENTS.md「Full-height page layout」：grid lg:h-full 让左右两列等高、
          与答题页原文卡片同高；每列的主卡片 flex-1 撑满，内容在 CardContent 内滚动，
          内容不足时用 min-h-full + justify-center 居中。次级卡片 shrink-0 + 限高滚动。 */}
      <div className="grid gap-6 lg:h-full lg:min-h-0 lg:grid-cols-[1fr_380px]">
        <div className="flex min-h-0 flex-col gap-6 lg:overflow-hidden">
          {!graded ? (
            <Card className="flex min-h-0 flex-1 flex-col border-border shadow-none">
              <CardHeader className="shrink-0">
                <CardTitle className="text-base">
                  {gradingInFlight
                    ? "正在批改"
                    : s.status === SubmissionStatus.grading_failed
                      ? "批改失败"
                      : "尚未批改"}
                </CardTitle>
              </CardHeader>
              <CardContent className="min-h-0 flex-1 overflow-y-auto">
                {gradingInFlight ? (
                  // 批改期间界面稳定停在这里，不随每次轮询变化。后端跑完（graded）或失败
                  // （grading_failed）时才整块换掉——这就是"结果回来了再刷新显示"。
                  <div className="flex min-h-full flex-col items-center justify-center gap-3 py-10 text-center">
                    <Loader2 className="size-8 animate-spin text-primary" />
                    <p className="text-sm font-medium">AI 正在批改</p>
                    <p className="max-w-md text-sm text-muted-foreground">
                      四个阶段：逐句取证、中文校对、复筛补漏，最后依官方 Band 定档。通常 2-3 分钟，
                      完成后本页会自动显示结果，期间可以离开再回来。
                    </p>
                    {takingLong ? (
                      <p className="max-w-md text-xs text-warning-foreground">
                        比平常久一些，后端仍在运行（状态还是「批改中」）。请继续等待——
                        失败时会自动转为「批改失败」并允许重新发起。
                      </p>
                    ) : null}
                    {handoffStuck ? (
                      <p className="max-w-md text-xs text-warning-foreground">
                        任务已提交但后台还没开始处理，可能是后台服务未就绪。稍候仍无变化的话，
                        请检查后端是否正常运行。
                      </p>
                    ) : null}
                  </div>
                ) : (
                  <div className="flex flex-col items-start gap-3">
                    <p className="text-sm text-muted-foreground">
                      {s.status === SubmissionStatus.grading_failed
                        ? "上一次批改未完成。后端已经自行重试过（每阶段最多 3 次）仍未成功，可以手动重新发起。"
                        : "提交已记录，点击下方按钮开始 AI 批改。"}
                    </p>
                    <Button disabled={!examType.data} onClick={() => grade.mutate()}>
                      <Gavel className="size-4" />
                      {s.status === SubmissionStatus.grading_failed ? "重新批改" : "开始批改"}
                    </Button>
                    <AiLoadingState
                      status={grade.status}
                      error={grade.error}
                      pendingHint="正在提交批改任务"
                    />
                  </div>
                )}
              </CardContent>
            </Card>
          ) : (
            <div className="min-h-0 flex-1 lg:overflow-y-auto">
              <div className="flex min-h-full flex-col justify-center">
                <GradingResultPanel submission={s} />
              </div>
            </div>
          )}

          {s.status === SubmissionStatus.standard_revised ? (
            <div className="shrink-0 rounded-lg border border-success/40 bg-success/8 p-4 text-sm">
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

        <div className="flex min-h-0 flex-col gap-6 lg:overflow-hidden">
          <Card className="flex min-h-0 flex-1 flex-col border-border shadow-none">
            <CardHeader className="shrink-0">
              <CardTitle className="text-base">你的作答</CardTitle>
            </CardHeader>
            <CardContent className="min-h-0 flex-1 overflow-y-auto">
              <div className="flex min-h-full flex-col justify-center">
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
              </div>
            </CardContent>
          </Card>

          {question.data?.meaningCheckpoints.length ? (
            <Card className="shrink-0 border-border shadow-none lg:max-h-[32%] lg:overflow-y-auto">
              <CardHeader>
                <CardTitle className="text-base">核心意义点</CardTitle>
              </CardHeader>
              <CardContent>
                <ul className="space-y-1">
                  {question.data.meaningCheckpoints.map((c) => (
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
              </CardContent>
            </Card>
          ) : null}

          {/* 追问历史现在完整呈现在 FollowUpPanel（SidePanel）里，页面不再单独放一份记录卡片。 */}
        </div>
      </div>
    </AppShell>
  );
}
