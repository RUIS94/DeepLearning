"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { MessageCircleQuestion, Plus, Scale, Send } from "lucide-react";
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
import { AiLoadingState } from "@/components/shared/ai-loading-state";
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

const NEW = "__new__";

function verdictLabel(v: number | null): string {
  switch (v) {
    case FollowUpVerdict.user_correct:
      return "用户判断正确";
    case FollowUpVerdict.user_incorrect:
      return "维持原判";
    case FollowUpVerdict.partial:
      return "部分成立";
    default:
      return "已答复";
  }
}

function threadRowLabel(t: FollowUpThreadSummary): string {
  const prefix = t.kind === FollowUpThreadKind.score_challenge ? "改判 · " : "";
  return (
    prefix + (t.status === FollowUpThreadStatus.open ? "进行中" : verdictLabel(t.finalVerdict))
  );
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
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState<string | null>(null); // thread id | NEW | null(未初始化)
  const [text, setText] = useState("");
  const [confirmingClose, setConfirmingClose] = useState(false);
  const [draft, setDraft] = useState<FollowUpCloseInput | null>(null);
  const [draftMeta, setDraftMeta] = useState<FollowUpClosePreview | null>(null);
  const examType = useExamType();
  const currentUser = useCurrentUser();
  const queryClient = useQueryClient();
  const bottomRef = useRef<HTMLDivElement>(null);

  const threads = useQuery({
    queryKey: ["follow-up-threads", submissionId],
    queryFn: () => listFollowUpThreads(submissionId),
    // The default-trigger instance needs to know upfront whether a thread is already open
    // (so its button can say "查看进行中的追问"); the per-item instances only need it once opened.
    enabled: open || renderTrigger === undefined,
  });

  const openThread = threads.data?.find((t) => t.status === FollowUpThreadStatus.open) ?? null;

  useEffect(() => {
    if (open && active === null && threads.data) {
      setActive(openThread ? openThread.id : NEW);
    }
  }, [open, active, threads.data, openThread]);

  // `active` is component-local — a stale thread id can survive (react-query cache kept a
  // stale detail, the DB was reset, etc.). Once the list is known, if `active` points at a
  // thread that isn't in it, drop back to the open thread / compose instead of previewing or
  // closing a ghost id (which 404s).
  useEffect(() => {
    if (
      threads.data &&
      active !== null &&
      active !== NEW &&
      !threads.data.some((t) => t.id === active)
    ) {
      setActive(openThread ? openThread.id : NEW);
      setText("");
      setDraft(null);
      setDraftMeta(null);
      setConfirmingClose(false);
    }
  }, [threads.data, active, openThread]);

  const composing = active === NEW;
  const viewingThreadId = composing || active === null ? null : active;

  const detail = useQuery({
    queryKey: ["follow-up-thread", viewingThreadId],
    queryFn: () => getFollowUpThread(viewingThreadId!),
    enabled: open && !!viewingThreadId,
    retry: false,
  });

  function applyResult(data: FollowUpThreadDetail) {
    queryClient.setQueryData(["follow-up-thread", data.id], data);
    queryClient.invalidateQueries({ queryKey: ["follow-up-threads", submissionId] });
    queryClient.invalidateQueries({ queryKey: ["submission", submissionId] });
    onChanged?.();
  }

  const send = useMutation({
    mutationFn: () =>
      composing
        ? createFollowUpThread({
            submissionId,
            userId: currentUser.data!.id,
            examTypeId: examType.data!.id,
            contextRef: disputeAnchor ? disputeAnchor.contextRef : null,
            questionText: text,
            kind: scoreChallenge ? FollowUpThreadKind.score_challenge : null,
            dimensionId: scoreChallenge ? scoreChallenge.dimensionId : null,
          })
        : addFollowUpMessage(detail.data!.id, {
            userId: currentUser.data!.id,
            questionText: text,
          }),
    onSuccess: (data) => {
      applyResult(data);
      setText("");
      setActive(data.id);
    },
  });

  const preview = useMutation({
    mutationFn: () => previewFollowUpClose(detail.data!.id, currentUser.data!.id),
    onSuccess: (p) => {
      setDraftMeta(p);
      setDraft(previewToInput(p));
    },
  });

  const close = useMutation({
    mutationFn: (input?: FollowUpCloseInput) =>
      closeFollowUpThread(detail.data!.id, currentUser.data!.id, input),
    onSuccess: (data) => {
      applyResult(data);
      setConfirmingClose(false);
      setDraft(null);
      setDraftMeta(null);
    },
  });

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ block: "end" });
  }, [detail.data?.messages.length, send.isPending, composing, draft]);

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

  return (
    <>
      {renderTrigger ? (
        renderTrigger(() => setOpen(true))
      ) : (
        <Button
          variant={openThread ? "default" : "outline"}
          onClick={() => setOpen(true)}
          title={openThread ? "本提交有一条进行中的追问" : undefined}
        >
          {isScoreChallenge ? (
            <Scale className="size-4" />
          ) : (
            <MessageCircleQuestion className="size-4" />
          )}
          {openThread
            ? openThread.kind === FollowUpThreadKind.score_challenge
              ? "查看进行中的改判申请"
              : "查看进行中的追问"
            : isScoreChallenge
              ? "申请改判"
              : "对判定有异议 / 有疑问？发起追问"}
        </Button>
      )}
      <SidePanel open={open} onOpenChange={setOpen}>
        <SidePanelContent width="30rem">
          <SidePanelHeader
            title={isScoreChallenge ? `申请改判 · ${scoreChallenge!.dimensionKey}` : "追问"}
            description={
              isScoreChallenge
                ? "说明这个维度的 Band 为什么应当调整；结束申请后由你确认 AI 结算草稿再落库。"
                : disputeAnchor
                  ? `针对「${disputeAnchor.label}」提出异议或疑问。`
                  : "请在此处提出任何疑问。"
            }
          />
          <SidePanelBody className="flex flex-col gap-4">
            {threads.data && threads.data.length > 0 ? (
              <div className="flex flex-wrap gap-1.5">
                {threads.data
                  .slice()
                  .reverse()
                  .map((t, i) => (
                    <button
                      key={t.id}
                      type="button"
                      onClick={() => {
                        setActive(t.id);
                        setConfirmingClose(false);
                        setDraft(null);
                        setDraftMeta(null);
                      }}
                      className={cn(
                        "rounded-full border px-2.5 py-1 text-xs transition-colors",
                        active === t.id ? CHIP_ON : CHIP_OFF,
                      )}
                    >
                      #{i + 1} · {threadRowLabel(t)}
                    </button>
                  ))}
                <button
                  type="button"
                  disabled={!canStartNew}
                  title={canStartNew ? undefined : "结束当前追问后才能发起新的"}
                  onClick={() => {
                    setActive(NEW);
                    setText("");
                    setConfirmingClose(false);
                    setDraft(null);
                    setDraftMeta(null);
                  }}
                  className={cn(
                    "inline-flex items-center gap-1 rounded-full border px-2.5 py-1 text-xs transition-colors",
                    composing
                      ? CHIP_ON
                      : "border-dashed border-border text-muted-foreground hover:bg-secondary",
                    !canStartNew && "cursor-not-allowed opacity-40 hover:bg-transparent",
                  )}
                >
                  <Plus className="size-3" />
                  新追问
                </button>
              </div>
            ) : null}

            {composing ? (
              <p className="text-sm text-muted-foreground">
                {threads.data && threads.data.length > 0
                  ? "开始一次新的追问，可以和之前的追问无关。"
                  : "说明你的疑问——可以是对某条判定的异议，也可以是想弄懂的知识点。"}
              </p>
            ) : null}

            {viewingThreadId && detail.isError ? (
              <div className="flex flex-col gap-2 rounded-lg border border-warning/40 bg-warning/10 p-3 text-sm">
                <span>这条追问线程已不存在（可能已在别处关闭，或本地数据被重置）。</span>
                <Button
                  size="sm"
                  variant="outline"
                  className="self-start"
                  onClick={() => {
                    setActive(openThread ? openThread.id : NEW);
                    setDraft(null);
                    setDraftMeta(null);
                    setConfirmingClose(false);
                    queryClient.invalidateQueries({
                      queryKey: ["follow-up-threads", submissionId],
                    });
                  }}
                >
                  {openThread ? "打开进行中的追问" : "发起新的追问"}
                </Button>
              </div>
            ) : null}

            {isViewingClosed && viewedThread ? (
              <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border bg-secondary/50 p-3">
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
                      ? "已改判：该维度分数已调整"
                      : "维持原判：分数未变"}
                  </Badge>
                ) : (
                  <>
                    <Badge
                      variant="outline"
                      className={
                        viewedThread.finalVerdict === FollowUpVerdict.user_correct
                          ? "border-transparent bg-success/12 text-success"
                          : "border-transparent bg-warning/20 text-warning-foreground"
                      }
                    >
                      {viewedThread.finalVerdict === null
                        ? "已答复（未涉及评判争议）"
                        : `最终结论：${verdictLabel(viewedThread.finalVerdict)}`}
                    </Badge>
                    {viewedThread.standardOverrideStatus !== null ? (
                      <Badge variant="outline" className="border-primary/30 text-primary">
                        已生成评分标准修正记录
                      </Badge>
                    ) : null}
                  </>
                )}
              </div>
            ) : null}

            {viewedThread?.messages.map((m) => (
              <div
                key={m.id}
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
                      : "border border-border bg-secondary/50",
                  )}
                >
                  {m.content}
                </div>
                {m.role === FollowUpMessageRole.ai && m.verdict !== null ? (
                  <Badge variant="outline" className="border-border text-xs text-muted-foreground">
                    {verdictLabel(m.verdict)}
                  </Badge>
                ) : null}
              </div>
            ))}

            <AiLoadingState
              status={send.status}
              error={send.error}
              pendingHint="AI 正在回复，可能需要几秒到十几秒"
            />
            <AiLoadingState
              status={preview.status}
              error={preview.error}
              pendingHint="AI 正在综合整个对话生成结算草稿"
            />
            <AiLoadingState status={close.status} error={close.error} pendingHint="正在落库…" />

            <div ref={bottomRef} />
          </SidePanelBody>

          {showComposer ? (
            <SidePanelFooter className="flex-col items-stretch gap-2">
              {draft && draftMeta && viewedThread ? (
                <CloseReviewDraft
                  kind={viewedThread.kind}
                  meta={draftMeta}
                  draft={draft}
                  onChange={updateDraft}
                  committing={close.isPending}
                  regenerating={preview.isPending}
                  onConfirm={() => close.mutate(draft)}
                  onRegenerate={() => preview.mutate()}
                  onDiscard={() => {
                    setDraft(null);
                    setDraftMeta(null);
                  }}
                />
              ) : confirmingClose ? (
                <div className="flex items-center justify-between gap-3 rounded-lg border border-warning/40 bg-warning/10 p-3 text-sm">
                  <span>结束后这条追问不能再继续，确定吗？</span>
                  <div className="flex shrink-0 gap-2">
                    <Button size="sm" variant="outline" onClick={() => setConfirmingClose(false)}>
                      取消
                    </Button>
                    <Button
                      size="sm"
                      disabled={close.isPending}
                      onClick={() => close.mutate(undefined)}
                    >
                      {close.isPending ? "结束中…" : "确认结束"}
                    </Button>
                  </div>
                </div>
              ) : (
                <>
                  <Textarea
                    value={text}
                    onChange={(e) => setText(e.target.value)}
                    rows={3}
                    placeholder={
                      composing
                        ? isScoreChallenge
                          ? "例如：这个维度只有 1 处 Minor，按官方 Band 描述不该扣到 Band 3。"
                          : "例如：「语域不当」这条我不认同；或者：carer 在澳洲语境一般怎么翻译？"
                        : "继续追问……"
                    }
                  />
                  <div className="flex items-center justify-between gap-2">
                    {isViewingOpen ? (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={send.isPending || preview.isPending || close.isPending}
                        onClick={() => (needsSummary ? preview.mutate() : setConfirmingClose(true))}
                      >
                        {viewedKind === FollowUpThreadKind.score_challenge
                          ? "结束申请"
                          : "结束追问"}
                      </Button>
                    ) : (
                      <span />
                    )}
                    <Button
                      size="sm"
                      disabled={
                        send.isPending ||
                        text.trim().length < 5 ||
                        !examType.data ||
                        !currentUser.data
                      }
                      onClick={() => send.mutate()}
                    >
                      <Send className="size-4" />
                      {send.isPending ? "发送中…" : "发送"}
                    </Button>
                  </div>
                </>
              )}
            </SidePanelFooter>
          ) : null}
        </SidePanelContent>
      </SidePanel>
    </>
  );
}

function CloseReviewDraft({
  kind,
  meta,
  draft,
  onChange,
  onConfirm,
  onRegenerate,
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
  onDiscard: () => void;
  committing: boolean;
  regenerating: boolean;
}) {
  const isScore = kind === FollowUpThreadKind.score_challenge;
  const committable = inputIsCommittable(kind, draft);

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-primary/30 bg-primary/5 p-3 text-sm">
      <p className="text-xs font-medium text-primary">AI 结算草稿 —— 确认或修改后再落库</p>

      <label className="flex flex-col gap-1">
        <span className="text-xs text-muted-foreground">结论说明</span>
        <Textarea
          rows={4}
          value={draft.aiResponse}
          onChange={(e) => onChange({ aiResponse: e.target.value })}
        />
      </label>

      {isScore ? (
        <>
          <div className="flex flex-col gap-1">
            <span className="text-xs text-muted-foreground">
              裁决{meta.currentBand !== null ? `（当前 Band ${meta.currentBand}）` : ""}
            </span>
            <div className="flex gap-1.5">
              {[
                { v: ScoreChallengeDecision.uphold, label: "维持原判" },
                { v: ScoreChallengeDecision.adjust, label: "改判" },
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
                <span className="text-xs text-muted-foreground">改判后 Band</span>
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
                <span className="text-xs text-muted-foreground">改判依据</span>
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
          <div className="flex flex-col gap-1">
            <span className="text-xs text-muted-foreground">最终结论</span>
            <div className="flex flex-wrap gap-1.5">
              {[
                { v: FollowUpVerdict.user_correct, label: "用户正确" },
                { v: FollowUpVerdict.user_incorrect, label: "维持原判" },
                { v: FollowUpVerdict.partial, label: "部分成立" },
                { v: null, label: "无争议" },
              ].map((o) => (
                <button
                  key={String(o.v)}
                  type="button"
                  className={cn(CHIP, draft.finalVerdict === o.v ? CHIP_ON : CHIP_OFF)}
                  onClick={() =>
                    onChange({
                      finalVerdict: o.v,
                      standardRevision:
                        o.v === FollowUpVerdict.user_correct
                          ? (draft.standardRevision ?? {
                              scope: OverrideScope.grading_rubric,
                              dimensionOrRule: "",
                              originalRuleText: null,
                              revisedRuleText: "",
                            })
                          : null,
                    })
                  }
                >
                  {o.label}
                </button>
              ))}
            </div>
          </div>
          {draft.finalVerdict === FollowUpVerdict.user_correct && draft.standardRevision ? (
            <div className="flex flex-col gap-2 rounded-md border border-border bg-background p-2">
              <span className="text-xs font-medium">评分标准修正记录</span>
              <div className="flex gap-1.5">
                {[
                  { v: OverrideScope.grading_rubric, label: "评分维度" },
                  { v: OverrideScope.translation_reference, label: "原文/参考译文" },
                ].map((o) => (
                  <button
                    key={o.v}
                    type="button"
                    className={cn(CHIP, draft.standardRevision!.scope === o.v ? CHIP_ON : CHIP_OFF)}
                    onClick={() =>
                      onChange({ standardRevision: { ...draft.standardRevision!, scope: o.v } })
                    }
                  >
                    {o.label}
                  </button>
                ))}
              </div>
              <Input
                placeholder="dimensionOrRule（维度 key 或问题标识）"
                className="h-8"
                value={draft.standardRevision.dimensionOrRule}
                onChange={(e) =>
                  onChange({
                    standardRevision: {
                      ...draft.standardRevision!,
                      dimensionOrRule: e.target.value,
                    },
                  })
                }
              />
              <Input
                placeholder="originalRuleText（这次错在哪，可留空）"
                className="h-8"
                value={draft.standardRevision.originalRuleText ?? ""}
                onChange={(e) =>
                  onChange({
                    standardRevision: {
                      ...draft.standardRevision!,
                      originalRuleText: e.target.value || null,
                    },
                  })
                }
              />
              <Textarea
                rows={2}
                placeholder="revisedRuleText（以后该如何判）"
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
            </div>
          ) : null}
        </>
      )}

      <div className="flex flex-wrap items-center justify-end gap-2 pt-1">
        <Button variant="ghost" size="sm" disabled={committing || regenerating} onClick={onDiscard}>
          放弃
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={committing || regenerating}
          onClick={onRegenerate}
        >
          {regenerating ? "重新生成…" : "重新生成"}
        </Button>
        <Button size="sm" disabled={committing || regenerating || !committable} onClick={onConfirm}>
          {committing ? "落库中…" : "确认并结束"}
        </Button>
      </div>
    </div>
  );
}
