"use client";

import { useEffect, useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2, MessageCircleQuestion, Plus, Scale, Send, X } from "lucide-react";
import {
  SidePanel,
  SidePanelBody,
  SidePanelContent,
  SidePanelFooter,
  SidePanelHeader,
} from "@/components/ui/side-panel";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { ErrorBanner } from "@/components/shared/ai-loading-state";
import { TypingBubble } from "@/components/shared/typing-bubble";
import {
  addFollowUpMessage,
  closeFollowUpThread,
  createFollowUpThread,
  getFollowUpThread,
  listFollowUpThreads,
  previewFollowUpClose,
} from "@/lib/api/follow-up-threads";
import { useExamType } from "@/hooks/use-exam-config";
import { useCurrentUser } from "@/hooks/use-current-user";
import { useT, type TranslateFn } from "@/lib/i18n";
import {
  FollowUpMessageRole,
  FollowUpThreadKind,
  FollowUpThreadStatus,
  FollowUpVerdict,
  OverrideScope,
  ScoreChallengeDecision,
  SubmissionStatus,
} from "@/lib/types/enums";
import { cn } from "@/lib/utils";
import type {
  FollowUpCloseInput,
  FollowUpClosePreview,
  FollowUpThreadDetail,
  FollowUpThreadSummary,
} from "@/lib/types/dtos";
import { qk } from "@/lib/query-keys";

const NEW = "__new__";

function verdictLabel(translate: TranslateFn, v: number | null): string {
  switch (v) {
    case FollowUpVerdict.user_correct:
      return translate("followUp.verdict.userCorrect");
    case FollowUpVerdict.user_incorrect:
      return translate("followUp.verdict.upheld");
    case FollowUpVerdict.partial:
      return translate("followUp.verdict.partial");
    default:
      return translate("followUp.verdict.answered");
  }
}

function threadRowLabel(translate: TranslateFn, thread: FollowUpThreadSummary): string {
  const prefix =
    thread.kind === FollowUpThreadKind.score_challenge
      ? translate("followUp.rowPrefixChallenge")
      : "";
  return (
    prefix +
    (thread.status === FollowUpThreadStatus.open
      ? translate("followUp.rowInProgress")
      : verdictLabel(translate, thread.finalVerdict))
  );
}

/** 线程序号 chip 的状态色（open=进行中；closed 按 finalVerdict 分色）。文字描述走 tooltip。 */
function threadChipTone(thread: FollowUpThreadSummary): string {
  if (thread.status === FollowUpThreadStatus.open) return "bg-warning";
  switch (thread.finalVerdict) {
    case FollowUpVerdict.user_correct:
      return "bg-success";
    case FollowUpVerdict.partial:
      return "bg-warning";
    case FollowUpVerdict.user_incorrect:
      return "bg-muted-foreground";
    default:
      return "bg-primary";
  }
}

/** 结算 / 保存进行中的等待态：与"AI 正在输入"一致的气泡（bg-muted 跳动圆点）+ 一句提示。出错回退到 ErrorBanner。 */
function ChatWaiting({
  status,
  error,
  hint,
}: {
  status: "idle" | "pending" | "success" | "error";
  error?: unknown;
  hint: string;
}) {
  if (status === "pending") {
    return (
      <div className="flex flex-col items-start gap-1.5">
        <TypingBubble />
        <span className="text-xs text-muted-foreground">{hint}</span>
      </div>
    );
  }
  if (status === "error" && error) {
    return <ErrorBanner error={error} />;
  }
  return null;
}

type ScoreChallengeTarget = { dimensionId: string; dimensionKey: string };
type DisputeAnchor = { contextRef: string; label: string };

function previewToInput(p: FollowUpClosePreview): FollowUpCloseInput {
  return {
    aiResponse: p.aiResponse,
    finalVerdict: p.finalVerdict,
    standardRevision: p.standardRevision,
    decision: p.decision,
    revisedBand: p.revisedBand,
    revisedRationale: p.revisedRationale,
  };
}

/** 结算草稿是否可以落库（用户可能改成不完整的状态）。 */
function inputIsCommittable(kind: number, i: FollowUpCloseInput): boolean {
  if (!i.aiResponse.trim()) return false;
  if (kind === FollowUpThreadKind.score_challenge) {
    if (i.decision !== ScoreChallengeDecision.adjust) return true;
    return (
      i.revisedBand !== null &&
      i.revisedBand >= 1 &&
      i.revisedBand <= 5 &&
      !!i.revisedRationale?.trim()
    );
  }
  if (i.finalVerdict !== FollowUpVerdict.user_correct) return true;
  const r = i.standardRevision;
  return !!r && !!r.dimensionOrRule.trim() && !!r.revisedRuleText.trim();
}

const CHIP = "rounded-md border px-2 py-1 text-xs transition-colors";
const CHIP_ON = "border-primary bg-primary/10 text-primary";
const CHIP_OFF = "border-border text-muted-foreground hover:bg-secondary";

/**
 * 追问面板（SidePanel，非模态）。一个 submission 可有多条追问线程：同时只有一条"进行中"，
 * 结束后可再发起新的、与上次无关的追问，历史线程都保留可回看。
 *
 * scoreChallenge 非空 → "从分数区对某维度 Band 发起改判申请"模式。
 * disputeAnchor 非空 → "从错误清单引用某条来质疑"模式（新线程带 contextRef，后端据此走 dispute）。
 *
 * dispute / score_challenge 线程"结束"时不直接落库：先 previewFollowUpClose 出 AI 草稿，
 * 用户确认 / 编辑 / 重新生成 / 放弃，确认后才把（可能改过的）结果回传 close。
 * 纯 knowledge 线程没有结算，"结束"就是直接关闭。
 */
export function FollowUpPanel({
  submissionId,
  onChanged,
  scoreChallenge = null,
  disputeAnchor = null,
  renderTrigger,
}: {
  submissionId: string;
  onChanged?: () => void;
  scoreChallenge?: ScoreChallengeTarget | null;
  disputeAnchor?: DisputeAnchor | null;
  renderTrigger?: (openPanel: () => void) => ReactNode;
}) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState<string | null>(null); // thread id | NEW | null(未初始化)
  const [text, setText] = useState("");
  // 已发出、正在等待回复的那条消息 —— 输入框在提交瞬间就清空，乐观气泡渲染这个快照。
  const [sentText, setSentText] = useState("");
  // false = not confirming; "normal" = 结束并（若需要）生成结算；"skip" = 直接结束、不生成任何结算。
  const [confirmingClose, setConfirmingClose] = useState<false | "normal" | "skip">(false);
  const [draft, setDraft] = useState<FollowUpCloseInput | null>(null);
  const [draftMeta, setDraftMeta] = useState<FollowUpClosePreview | null>(null);
  // 结算草稿面板被临时收起（点 X）——草稿内容仍保留在 draft / draftMeta 里，可原样恢复，不必重新生成。
  const [draftCollapsed, setDraftCollapsed] = useState(false);

  // 彻底丢弃草稿（"放弃"按钮 / 切换线程 / 关闭成功后）。
  function clearDraft() {
    setDraft(null);
    setDraftMeta(null);
    setDraftCollapsed(false);
  }
  const examType = useExamType();
  const currentUser = useCurrentUser();
  const queryClient = useQueryClient();
  const bottomRef = useRef<HTMLDivElement>(null);
  const lastAiMsgRef = useRef<HTMLDivElement>(null);
  const composerRef = useRef<HTMLTextAreaElement>(null);

  const threads = useQuery({
    queryKey: qk.followUpThreads(submissionId),
    queryFn: () => listFollowUpThreads(submissionId),
    // The default-trigger instance needs to know upfront whether a thread is already open
    // (so its button can say "查看进行中的追问"); the per-item instances only need it once opened.
    enabled: open || renderTrigger === undefined,
  });

  const openThread = threads.data?.find((th) => th.status === FollowUpThreadStatus.open) ?? null;

  useEffect(() => {
    if (open && active === null && threads.data) {
      setActive(openThread ? openThread.id : NEW);
    }
  }, [open, active, threads.data, openThread]);

  // `active` is component-local — a stale thread id can survive (react-query cache kept a
  // stale detail, the DB was reset, etc.). Once the list is known, if `active` points at a
  // thread that isn't in it, drop back to the open thread / compose instead of previewing or
  // closing a ghost id (which 404s).
  // Guard on `!threads.isFetching`: right after sending the first message of a new thread we
  // `setActive(newId)` and invalidate this list, so for one render `threads.data` is the stale
  // pre-create list that legitimately lacks `newId` — without the guard we'd immediately bounce
  // off the freshly created thread back to the composer / some other open thread.
  useEffect(() => {
    if (
      threads.data &&
      !threads.isFetching &&
      active !== null &&
      active !== NEW &&
      !threads.data.some((th) => th.id === active)
    ) {
      setActive(openThread ? openThread.id : NEW);
      setText("");
      setDraft(null);
      setDraftMeta(null);
      setDraftCollapsed(false);
      setConfirmingClose(false);
    }
  }, [threads.data, threads.isFetching, active, openThread]);

  const composing = active === NEW;
  const viewingThreadId = composing || active === null ? null : active;

  const detail = useQuery({
    queryKey: qk.followUpThread(viewingThreadId),
    queryFn: () => getFollowUpThread(viewingThreadId!),
    enabled: open && !!viewingThreadId,
    retry: false,
  });

  function applyResult(data: FollowUpThreadDetail) {
    queryClient.setQueryData(qk.followUpThread(data.id), data);
    queryClient.invalidateQueries({ queryKey: qk.followUpThreads(submissionId) });
    queryClient.invalidateQueries({ queryKey: qk.submission(submissionId) });
    onChanged?.();
  }

  const send = useMutation({
    mutationFn: (questionText: string) =>
      composing
        ? createFollowUpThread({
            submissionId,
            userId: currentUser.data!.id,
            examTypeId: examType.data!.id,
            contextRef: disputeAnchor ? disputeAnchor.contextRef : null,
            questionText,
            kind: scoreChallenge ? FollowUpThreadKind.score_challenge : null,
            dimensionId: scoreChallenge ? scoreChallenge.dimensionId : null,
          })
        : addFollowUpMessage(detail.data!.id, {
            userId: currentUser.data!.id,
            questionText,
          }),
    onSuccess: (data) => {
      applyResult(data);
      setActive(data.id);
    },
    // 发送失败时把刚清空的输入还回去（除非用户已经开始输入新的内容）。
    onError: (_err, questionText) => {
      setText((cur) => (cur.length > 0 ? cur : questionText));
    },
  });

  const preview = useMutation({
    mutationFn: () => previewFollowUpClose(detail.data!.id, currentUser.data!.id),
    onSuccess: (p) => {
      setDraftMeta(p);
      setDraft(previewToInput(p));
      setDraftCollapsed(false);
    },
  });

  const close = useMutation({
    mutationFn: (opts?: { input?: FollowUpCloseInput; skipSummary?: boolean }) =>
      closeFollowUpThread(
        detail.data!.id,
        currentUser.data!.id,
        opts?.input,
        opts?.skipSummary ?? false,
      ),
    onSuccess: (data) => {
      applyResult(data);
      setConfirmingClose(false);
      clearDraft();
    },
  });

  const messages = detail.data?.messages ?? [];
  const aiReplyCount = messages.filter((m) => m.role === FollowUpMessageRole.ai).length;
  const lastMessageIsAi = messages[messages.length - 1]?.role === FollowUpMessageRole.ai;

  // 发送 / 生成结算 / 落库进行中：贴到底部，让那排跳动圆点进入视野。
  // AI 回复落地后：滚到这条回复的顶部，方便从头读起，而不是一路冲到长回复的末尾。
  useEffect(() => {
    if (send.isPending || preview.isPending || close.isPending) {
      bottomRef.current?.scrollIntoView({ block: "end", behavior: "smooth" });
    } else if (lastMessageIsAi && lastAiMsgRef.current) {
      lastAiMsgRef.current.scrollIntoView({ block: "start", behavior: "smooth" });
    } else {
      bottomRef.current?.scrollIntoView({ block: "end" });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [
    aiReplyCount,
    messages.length,
    send.isPending,
    preview.isPending,
    close.isPending,
    composing,
    draft,
  ]);

  const viewedThread = detail.data;
  const isViewingOpen = viewedThread?.status === FollowUpThreadStatus.open;
  const isViewingClosed = viewedThread?.status === FollowUpThreadStatus.closed;
  const showComposer = composing || isViewingOpen;
  const canStartNew = !openThread;
  const isScoreChallenge = scoreChallenge !== null;
  const viewedKind = viewedThread?.kind ?? null;
  const needsSummary =
    viewedKind === FollowUpThreadKind.dispute || viewedKind === FollowUpThreadKind.score_challenge;

  function updateDraft(patch: Partial<FollowUpCloseInput>) {
    setDraft((d) => (d ? { ...d, ...patch } : d));
  }

  // Auto-grow the composer up to a cap, then let it scroll internally.
  useEffect(() => {
    const el = composerRef.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(el.scrollHeight, 160)}px`;
  }, [text, showComposer, draft, confirmingClose]);

  const canSend =
    !send.isPending && text.trim().length >= 5 && !!examType.data && !!currentUser.data;

  function submitMessage() {
    if (!canSend) return;
    setSentText(text);
    send.mutate(text);
    setText(""); // 立刻清空输入框，不等 AI 返回
  }

  function handleComposerKeyDown(e: KeyboardEvent<HTMLTextAreaElement>) {
    // Enter sends; Shift+Enter inserts a newline. Ignore Enter while an IME
    // composition is active (Chinese/Japanese input) so it only commits text.
    if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) {
      e.preventDefault();
      submitMessage();
    }
  }

  return (
    <>
      {renderTrigger ? (
        renderTrigger(() => setOpen(true))
      ) : (
        <Button
          variant={openThread ? "secondary" : "outline"}
          onClick={() => setOpen(true)}
          title={openThread ? t("followUp.openTitle") : undefined}
        >
          {isScoreChallenge ? (
            <Scale className="size-4" />
          ) : (
            <MessageCircleQuestion className="size-4" />
          )}
          {openThread
            ? openThread.kind === FollowUpThreadKind.score_challenge
              ? t("followUp.viewOngoingChallenge")
              : t("followUp.viewOngoing")
            : isScoreChallenge
              ? t("followUp.requestRegrade")
              : t("followUp.startFollowUp")}
        </Button>
      )}
      <SidePanel open={open} onOpenChange={setOpen}>
        <SidePanelContent width="30rem">
          <SidePanelHeader
            title={
              isScoreChallenge
                ? t("followUp.headerChallenge", { dim: scoreChallenge!.dimensionKey })
                : t("followUp.headerFollowUp")
            }
            description={
              isScoreChallenge
                ? t("followUp.descChallenge")
                : disputeAnchor
                  ? t("followUp.descDispute", { label: disputeAnchor.label })
                  : t("followUp.descDefault")
            }
          />
          <SidePanelBody className="flex flex-col gap-4">
            {composing ? (
              <p className="text-sm text-muted-foreground">
                {threads.data && threads.data.length > 0
                  ? t("followUp.newThreadHintHasPrev")
                  : t("followUp.newThreadHintFirst")}
              </p>
            ) : null}

            {viewingThreadId && detail.isError ? (
              <div className="flex flex-col gap-2 rounded-lg border border-warning/40 bg-warning/10 p-3 text-sm">
                <span>{t("followUp.threadGone")}</span>
                <Button
                  size="sm"
                  variant="outline"
                  className="self-start"
                  onClick={() => {
                    setActive(openThread ? openThread.id : NEW);
                    clearDraft();
                    setConfirmingClose(false);
                    queryClient.invalidateQueries({
                      queryKey: qk.followUpThreads(submissionId),
                    });
                  }}
                >
                  {openThread ? t("followUp.openInProgress") : t("followUp.startNew")}
                </Button>
              </div>
            ) : null}

            {isViewingClosed && viewedThread ? (
              <div className="flex flex-wrap items-center gap-2">
                {viewedThread.kind === FollowUpThreadKind.score_challenge ? (
                  <Badge
                    variant="outline"
                    className={
                      viewedThread.submissionStatus === SubmissionStatus.regraded
                        ? "border-transparent bg-success/12 text-success"
                        : "border-transparent bg-warning/20 text-warning-foreground"
                    }
                  >
                    {viewedThread.submissionStatus === SubmissionStatus.regraded
                      ? t("followUp.regradedBadge")
                      : t("followUp.upheldBadge")}
                  </Badge>
                ) : (
                  <>
                    <Badge
                      variant="outline"
                      className={
                        viewedThread.finalVerdict === FollowUpVerdict.user_correct
                          ? "border-transparent px-0 text-success"
                          : "border-transparent px-0 text-warning-foreground"
                      }
                    >
                      {viewedThread.finalVerdict === null
                        ? t("followUp.answeredNoDispute")
                        : t("followUp.finalVerdictPrefix", {
                            verdict: verdictLabel(t, viewedThread.finalVerdict),
                          })}
                    </Badge>
                    {viewedThread.standardOverrideStatus !== null ? (
                      <Badge variant="outline" className="border-primary/30 text-primary">
                        {t("followUp.standardRevisionGenerated")}
                      </Badge>
                    ) : null}
                  </>
                )}
              </div>
            ) : null}

            {viewedThread?.messages.map((m, mi, arr) => (
              <div
                key={m.id}
                ref={
                  m.role === FollowUpMessageRole.ai && mi === arr.length - 1
                    ? lastAiMsgRef
                    : undefined
                }
                className={cn(
                  "flex flex-col gap-1",
                  m.role === FollowUpMessageRole.user ? "items-end" : "items-start",
                )}
              >
                <div
                  className={cn(
                    "max-w-[85%] whitespace-pre-wrap rounded-lg px-3 py-2 text-sm leading-relaxed",
                    m.role === FollowUpMessageRole.user
                      ? "bg-primary text-primary-foreground"
                      : "bg-muted text-foreground",
                  )}
                >
                  {m.content}
                </div>
                {m.role === FollowUpMessageRole.ai && m.verdict !== null ? (
                  <Badge variant="outline" className="border-border text-xs text-muted-foreground">
                    {verdictLabel(t, m.verdict)}
                  </Badge>
                ) : null}
              </div>
            ))}

            {send.isPending ? (
              <>
                <div className="flex flex-col items-end gap-1">
                  <div className="max-w-[85%] whitespace-pre-wrap rounded-lg bg-primary px-3 py-2 text-sm leading-relaxed text-primary-foreground opacity-70">
                    {sentText}
                  </div>
                </div>
                <div className="flex items-start">
                  <TypingBubble />
                </div>
              </>
            ) : null}
            {send.isError ? <ErrorBanner error={send.error} /> : null}
            <ChatWaiting
              status={preview.status}
              error={preview.error}
              hint={t("followUp.aiDrafting")}
            />
            <ChatWaiting status={close.status} error={close.error} hint={t("followUp.saving")} />

            <div ref={bottomRef} />
          </SidePanelBody>

          {showComposer || (threads.data && threads.data.length > 0) ? (
            <SidePanelFooter className="flex-col items-stretch gap-2">
              {showComposer && confirmingClose ? (
                <div className="flex items-center justify-between gap-3 rounded-lg bg-warning/10 p-3 text-sm">
                  <span>
                    {confirmingClose === "skip"
                      ? t("followUp.confirmCloseNoSummaryWarning")
                      : t("followUp.confirmCloseWarning")}
                  </span>
                  <div className="flex shrink-0 gap-2">
                    <Button size="sm" variant="outline" onClick={() => setConfirmingClose(false)}>
                      {t("common.cancel")}
                    </Button>
                    <Button
                      size="sm"
                      variant="destructive"
                      disabled={close.isPending}
                      onClick={() =>
                        close.mutate(confirmingClose === "skip" ? { skipSummary: true } : {})
                      }
                    >
                      {close.isPending ? t("followUp.closing") : t("followUp.confirmClose")}
                    </Button>
                  </div>
                </div>
              ) : showComposer ? (
                <div className="relative flex items-end rounded-xl border border-input bg-transparent shadow-sm transition-colors focus-within:ring-1 focus-within:ring-ring">
                  <textarea
                    ref={composerRef}
                    value={text}
                    onChange={(e) => setText(e.target.value)}
                    onKeyDown={handleComposerKeyDown}
                    rows={1}
                    placeholder={
                      composing
                        ? isScoreChallenge
                          ? t("followUp.composerPlaceholderChallenge")
                          : t("followUp.composerPlaceholderNew")
                        : t("followUp.composerPlaceholderContinue")
                    }
                    className="max-h-[160px] min-h-[42px] w-full resize-none bg-transparent py-2.5 pl-3 pr-12 text-sm leading-relaxed placeholder:text-muted-foreground focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-50"
                  />
                  <Button
                    type="button"
                    size="icon"
                    className="absolute bottom-1.5 right-1.5 size-8 rounded-lg"
                    disabled={!canSend}
                    title={t("followUp.send")}
                    onClick={submitMessage}
                  >
                    {send.isPending ? (
                      <Loader2 className="size-4 animate-spin" />
                    ) : (
                      <Send className="size-4" />
                    )}
                  </Button>
                </div>
              ) : null}

              {threads.data && threads.data.length > 0 ? (
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <TooltipProvider delayDuration={200}>
                    <div className="flex flex-wrap items-center gap-1.5">
                      {threads.data
                        .slice()
                        .reverse()
                        .map((thread, i) => (
                          <Tooltip key={thread.id}>
                            <TooltipTrigger asChild>
                              <button
                                type="button"
                                onClick={() => {
                                  setActive(thread.id);
                                  setConfirmingClose(false);
                                  clearDraft();
                                }}
                                className={cn(
                                  "inline-flex cursor-pointer items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs transition-colors",
                                  active === thread.id
                                    ? "border-primary bg-primary/10 text-primary hover:bg-primary/15"
                                    : "border-border text-muted-foreground hover:bg-muted/50",
                                )}
                              >
                                <span
                                  className={cn("size-1.5 rounded-full", threadChipTone(thread))}
                                />
                                #{i + 1}
                              </button>
                            </TooltipTrigger>
                            <TooltipContent>{threadRowLabel(t, thread)}</TooltipContent>
                          </Tooltip>
                        ))}
                      <button
                        type="button"
                        disabled={!canStartNew}
                        title={
                          canStartNew ? t("followUp.newThread") : t("followUp.startNewAfterClose")
                        }
                        onClick={() => {
                          setActive(NEW);
                          setText("");
                          setConfirmingClose(false);
                          clearDraft();
                        }}
                        className={cn(
                          "inline-flex items-center gap-1 rounded-full border px-2.5 py-1 text-xs transition-colors",
                          composing
                            ? CHIP_ON
                            : "border-dashed border-border text-muted-foreground hover:bg-muted/50 cursor-pointer",
                          !canStartNew && "cursor-not-allowed opacity-40 hover:bg-transparent",
                        )}
                      >
                        <Plus className="size-3" />
                        {t("followUp.newThread")}
                      </button>
                    </div>
                  </TooltipProvider>
                  {showComposer && isViewingOpen && !confirmingClose ? (
                    draft && draftMeta && viewedThread ? (
                      draftCollapsed ? (
                        <div className="ml-auto flex shrink-0 items-center justify-end gap-2">
                          <Button
                            variant="outline"
                            size="sm"
                            disabled={close.isPending || preview.isPending}
                            onClick={() => setDraftCollapsed(false)}
                          >
                            {t("followUp.resumeDraft")}
                          </Button>
                        </div>
                      ) : null
                    ) : (
                      <div className="ml-auto flex shrink-0 items-center justify-end gap-2">
                        {needsSummary ? (
                          <Button
                            variant="ghost"
                            size="sm"
                            disabled={send.isPending || preview.isPending || close.isPending}
                            onClick={() => setConfirmingClose("skip")}
                          >
                            {t("followUp.closeNoSummary")}
                          </Button>
                        ) : null}
                        <Button
                          variant="outline"
                          size="sm"
                          disabled={send.isPending || preview.isPending || close.isPending}
                          onClick={() =>
                            needsSummary ? preview.mutate() : setConfirmingClose("normal")
                          }
                        >
                          {preview.isPending ? (
                            <>
                              <Loader2 className="size-4 animate-spin" />
                              {t("followUp.summarizing")}
                            </>
                          ) : viewedKind === FollowUpThreadKind.score_challenge ? (
                            t("followUp.closeRequest")
                          ) : (
                            t("followUp.closeFollowUp")
                          )}
                        </Button>
                      </div>
                    )
                  ) : null}
                </div>
              ) : null}
            </SidePanelFooter>
          ) : null}
        </SidePanelContent>
      </SidePanel>

      {/* 结算草稿：从追问面板左侧展开的等高子面板。点 X 只是临时收起（内容保留，可"继续编辑"恢复）；
          "放弃"按钮才真正清掉草稿。都不关闭追问面板本身。 */}
      {open && showComposer && draft && draftMeta && viewedThread && !draftCollapsed ? (
        <CloseReviewDraft
          kind={viewedThread.kind}
          meta={draftMeta}
          draft={draft}
          onChange={updateDraft}
          committing={close.isPending}
          regenerating={preview.isPending}
          onConfirm={() => close.mutate({ input: draft })}
          onRegenerate={() => preview.mutate()}
          onCollapse={() => setDraftCollapsed(true)}
          onDiscard={clearDraft}
        />
      ) : null}
    </>
  );
}

/** 结算草稿里的只读字段：AI 判定的部分用户不能改，只能确认 / 重新生成。 */
function DraftReadOnly({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-col gap-1">
      <span className="text-xs text-muted-foreground">{label}</span>
      <p className="whitespace-pre-wrap rounded-md bg-muted/40 px-3 py-2 text-sm text-foreground">
        {value || "—"}
      </p>
    </div>
  );
}

function CloseReviewDraft({
  kind,
  meta,
  draft,
  onChange,
  onConfirm,
  onRegenerate,
  onCollapse,
  onDiscard,
  committing,
  regenerating,
}: {
  kind: number;
  meta: FollowUpClosePreview;
  draft: FollowUpCloseInput;
  onChange: (patch: Partial<FollowUpCloseInput>) => void;
  onConfirm: () => void;
  onRegenerate: () => void;
  /** 点 X：临时收起，保留内容。 */
  onCollapse: () => void;
  /** "放弃"按钮：真正丢弃草稿。 */
  onDiscard: () => void;
  committing: boolean;
  regenerating: boolean;
}) {
  const t = useT();
  const isScore = kind === FollowUpThreadKind.score_challenge;
  const committable = inputIsCommittable(kind, draft);

  return (
    <div className="fixed inset-y-0 right-[30rem] z-50 flex h-dvh w-[26rem] max-w-[calc(100vw-30rem)] flex-col bg-background text-sm shadow-2xl duration-300 animate-in slide-in-from-right">
      <div className="flex shrink-0 items-start justify-between gap-3 px-5 py-4">
        <p className="text-sm font-medium text-primary">{t("followUp.draft.title")}</p>
        <button
          type="button"
          onClick={onCollapse}
          aria-label={t("common.close")}
          className="-mr-1 mt-0.5 shrink-0 rounded-md p-1 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground disabled:opacity-50"
        >
          <X className="size-4" />
        </button>
      </div>

      <div className="flex min-h-0 flex-1 flex-col gap-3 overflow-y-auto px-5 py-4">
        {/* AI 给出的结论：只读，用户只能确认 / 重新生成，不能改写。 */}
        <DraftReadOnly label={t("followUp.draft.conclusion")} value={draft.aiResponse} />

        {isScore ? (
          <>
            <div className="flex flex-col gap-1">
              <span className="text-xs text-muted-foreground">
                {meta.currentBand !== null
                  ? t("followUp.draft.decisionCurrentBand", { band: meta.currentBand })
                  : t("followUp.draft.decision")}
              </span>
              <div className="flex gap-1.5">
                {[
                  { v: ScoreChallengeDecision.uphold, label: t("followUp.draft.uphold") },
                  { v: ScoreChallengeDecision.adjust, label: t("followUp.draft.adjust") },
                ].map((o) => (
                  <button
                    key={o.v}
                    type="button"
                    className={cn(CHIP, draft.decision === o.v ? CHIP_ON : CHIP_OFF)}
                    onClick={() =>
                      onChange({
                        decision: o.v,
                        revisedBand:
                          o.v === ScoreChallengeDecision.adjust
                            ? (draft.revisedBand ?? meta.currentBand ?? null)
                            : null,
                        revisedRationale:
                          o.v === ScoreChallengeDecision.adjust ? draft.revisedRationale : null,
                      })
                    }
                  >
                    {o.label}
                  </button>
                ))}
              </div>
            </div>
            {draft.decision === ScoreChallengeDecision.adjust ? (
              <>
                <label className="flex items-center gap-2">
                  <span className="text-xs text-muted-foreground">
                    {t("followUp.draft.revisedBand")}
                  </span>
                  <Input
                    type="number"
                    min={1}
                    max={5}
                    className="h-8 w-20"
                    value={draft.revisedBand ?? ""}
                    onChange={(e) =>
                      onChange({
                        revisedBand: e.target.value === "" ? null : Number(e.target.value),
                      })
                    }
                  />
                </label>
                <label className="flex flex-col gap-1">
                  <span className="text-xs text-muted-foreground">
                    {t("followUp.draft.revisedRationale")}
                  </span>
                  <Textarea
                    rows={3}
                    value={draft.revisedRationale ?? ""}
                    onChange={(e) => onChange({ revisedRationale: e.target.value })}
                  />
                </label>
              </>
            ) : null}
          </>
        ) : (
          <>
            {/* AI 判定的最终结论：只读，label 与 value 同一行，按结论着色。 */}
            {(() => {
              const v = draft.finalVerdict;
              const verdict =
                v === FollowUpVerdict.user_correct
                  ? { text: t("followUp.draft.verdictUserCorrect"), tone: "text-success" }
                  : v === FollowUpVerdict.user_incorrect
                    ? { text: t("followUp.draft.verdictUpheld"), tone: "text-destructive" }
                    : v === FollowUpVerdict.partial
                      ? {
                          text: t("followUp.draft.verdictPartial"),
                          tone: "text-warning-foreground",
                        }
                      : {
                          text: t("followUp.draft.verdictNoDispute"),
                          tone: "text-muted-foreground",
                        };
              return (
                <div className="flex items-center gap-2">
                  <span className="text-xs text-muted-foreground">
                    {t("followUp.draft.finalVerdict")}
                  </span>
                  <span className={cn("text-sm font-medium", verdict.tone)}>{verdict.text}</span>
                </div>
              );
            })()}
            {draft.finalVerdict === FollowUpVerdict.user_correct && draft.standardRevision ? (
              <div className="flex flex-col gap-3">
                {/* scope + dimensionOrRule 由 AI 判定，只读，同一行显示，不需要 label。 */}
                <p className="text-sm">
                  <span className="text-muted-foreground">
                    {draft.standardRevision.scope === OverrideScope.translation_reference
                      ? t("followUp.draft.scopeReference")
                      : t("followUp.draft.scopeRubric")}
                  </span>
                  {" · "}
                  <span className="font-medium">
                    {draft.standardRevision.dimensionOrRule || "—"}
                  </span>
                </p>
                {/* 当时套用的规则原文：AI 提供，只读，无 label 无说明。 */}
                <p className="whitespace-pre-wrap rounded-md bg-muted/40 px-3 py-2 text-sm text-foreground">
                  {draft.standardRevision.originalRuleText || "—"}
                </p>
                <label className="flex flex-col gap-1">
                  <span className="text-xs font-medium">
                    {t("followUp.draft.revisedRuleLabel")}
                  </span>
                  <span className="text-xs text-muted-foreground">
                    {t("followUp.draft.revisedRuleHelp")}
                  </span>
                  <Textarea
                    rows={5}
                    className="mt-1"
                    value={draft.standardRevision.revisedRuleText}
                    onChange={(e) =>
                      onChange({
                        standardRevision: {
                          ...draft.standardRevision!,
                          revisedRuleText: e.target.value,
                        },
                      })
                    }
                  />
                </label>
              </div>
            ) : null}
          </>
        )}
      </div>

      <div className="flex shrink-0 flex-wrap items-center justify-end gap-2 px-5 py-4">
        <Button variant="ghost" size="sm" disabled={committing || regenerating} onClick={onDiscard}>
          {t("followUp.draft.discard")}
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={committing || regenerating}
          onClick={onRegenerate}
        >
          {regenerating ? t("followUp.draft.regenerating") : t("followUp.draft.regenerate")}
        </Button>
        <Button size="sm" disabled={committing || regenerating || !committable} onClick={onConfirm}>
          {committing ? t("followUp.draft.committing") : t("followUp.draft.confirmAndClose")}
        </Button>
      </div>
    </div>
  );
}
