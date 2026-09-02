"use client";

import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { MessageCircleQuestion, Plus, Send } from "lucide-react";
import {
  SidePanel,
  SidePanelBody,
  SidePanelContent,
  SidePanelFooter,
  SidePanelHeader,
} from "@/components/ui/side-panel";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { AiLoadingState } from "@/components/shared/ai-loading-state";
import {
  addFollowUpMessage,
  closeFollowUpThread,
  createFollowUpThread,
  getFollowUpThread,
  listFollowUpThreads,
} from "@/lib/api/follow-up-threads";
import { useExamType } from "@/hooks/use-exam-config";
import { useCurrentUser } from "@/hooks/use-current-user";
import { FollowUpMessageRole, FollowUpThreadStatus, FollowUpVerdict } from "@/lib/types/enums";
import { cn } from "@/lib/utils";
import type { FollowUpThreadDetail, FollowUpThreadSummary } from "@/lib/types/dtos";

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
  return t.status === FollowUpThreadStatus.open ? "进行中" : verdictLabel(t.finalVerdict);
}

/**
 * 追问面板（SidePanel，非模态）。一个 submission 可有多条追问线程：同时只有一条"进行中"，
 * 结束后可再发起新的、与上次无关的追问，历史线程都保留可回看。追问既能质疑评判、也能纯问
 * 知识点——是否产出"评分标准修正"由 AI 在结束时综合整个对话判断。
 */
export function FollowUpPanel({
  submissionId,
  onChanged,
}: {
  submissionId: string;
  onChanged?: () => void;
}) {
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState<string | null>(null); // thread id | NEW | null(未初始化)
  const [text, setText] = useState("");
  const [confirmingClose, setConfirmingClose] = useState(false);
  const examType = useExamType();
  const currentUser = useCurrentUser();
  const queryClient = useQueryClient();
  const bottomRef = useRef<HTMLDivElement>(null);

  const threads = useQuery({
    queryKey: ["follow-up-threads", submissionId],
    queryFn: () => listFollowUpThreads(submissionId),
    enabled: open,
  });

  const openThread = threads.data?.find((t) => t.status === FollowUpThreadStatus.open) ?? null;

  // 初次拿到列表时选中"进行中"的那条；没有就进入"新追问"编辑态。
  useEffect(() => {
    if (open && active === null && threads.data) {
      setActive(openThread ? openThread.id : NEW);
    }
  }, [open, active, threads.data, openThread]);

  const composing = active === NEW;
  const viewingThreadId = composing || active === null ? null : active;

  const detail = useQuery({
    queryKey: ["follow-up-thread", viewingThreadId],
    queryFn: () => getFollowUpThread(viewingThreadId!),
    enabled: open && !!viewingThreadId,
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
            contextRef: null,
            questionText: text,
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

  const close = useMutation({
    mutationFn: () => closeFollowUpThread(detail.data!.id, currentUser.data!.id),
    onSuccess: (data) => {
      applyResult(data);
      setConfirmingClose(false);
    },
  });

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ block: "end" });
  }, [detail.data?.messages.length, send.isPending, composing]);

  const viewedThread = detail.data;
  const isViewingOpen = viewedThread?.status === FollowUpThreadStatus.open;
  const isViewingClosed = viewedThread?.status === FollowUpThreadStatus.closed;
  const showComposer = composing || isViewingOpen;
  const canStartNew = !openThread;

  return (
    <>
      <Button variant="outline" onClick={() => setOpen(true)}>
        <MessageCircleQuestion className="size-4" />
        对判定有异议 / 有疑问？发起追问
      </Button>
      <SidePanel open={open} onOpenChange={setOpen}>
        <SidePanelContent width="30rem">
          <SidePanelHeader
            title="追问"
            description="请在此处提出任何疑问。"
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
                      }}
                      className={cn(
                        "rounded-full border px-2.5 py-1 text-xs transition-colors",
                        active === t.id
                          ? "border-primary bg-primary/10 text-primary"
                          : "border-border text-muted-foreground hover:bg-secondary",
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
                  }}
                  className={cn(
                    "inline-flex items-center gap-1 rounded-full border px-2.5 py-1 text-xs transition-colors",
                    composing
                      ? "border-primary bg-primary/10 text-primary"
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

            {isViewingClosed && viewedThread ? (
              <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border bg-secondary/50 p-3">
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
              status={close.status}
              error={close.error}
              pendingHint="AI 正在综合整个对话给出结论"
            />

            <div ref={bottomRef} />
          </SidePanelBody>

          {showComposer ? (
            <SidePanelFooter className="flex-col items-stretch gap-2">
              {confirmingClose ? (
                <div className="flex items-center justify-between gap-3 rounded-lg border border-warning/40 bg-warning/10 p-3 text-sm">
                  <span>结束后这条追问不能再继续，AI 会依据整个对话给出结论，确定吗？</span>
                  <div className="flex shrink-0 gap-2">
                    <Button size="sm" variant="outline" onClick={() => setConfirmingClose(false)}>
                      取消
                    </Button>
                    <Button size="sm" disabled={close.isPending} onClick={() => close.mutate()}>
                      {close.isPending ? "结算中…" : "确认结束"}
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
                        ? "例如：「语域不当」这条我不认同；或者：carer 在澳洲语境一般怎么翻译？"
                        : "继续追问……"
                    }
                  />
                  <div className="flex items-center justify-between gap-2">
                    {isViewingOpen ? (
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={send.isPending || close.isPending}
                        onClick={() => setConfirmingClose(true)}
                      >
                        结束追问
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
