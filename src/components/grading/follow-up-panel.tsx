"use client";

import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { MessageCircleQuestion, Send } from "lucide-react";
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
  getFollowUpThreadBySubmission,
} from "@/lib/api/follow-up-threads";
import { useExamType } from "@/hooks/use-exam-config";
import { useCurrentUser } from "@/hooks/use-current-user";
import {
  FollowUpMessageRole,
  FollowUpThreadStatus,
  FollowUpVerdict,
  FollowUpVerdictLabel,
  OverrideStatusLabel,
} from "@/lib/types/enums";
import { cn } from "@/lib/utils";
import type { FollowUpThreadDetail } from "@/lib/types/dtos";

/**
 * 追问面板——替代原来的一问一答 FollowUpDialog，走多轮对话：一个 submission 最多一条线程
 * （load-or-create），历史消息全部显示在这里，用户可以持续追问，最后手动点"结束追问"结算
 * 出最终结论（后端另跑一次总结调用，不是取某一轮的看法）。线程存续期间 submission 停在
 * under_dispute，见 submission-page.tsx 里 graded 判定的相应调整。
 */
export function FollowUpPanel({
  submissionId,
  onChanged,
}: {
  submissionId: string;
  onChanged?: () => void;
}) {
  const [open, setOpen] = useState(false);
  const [text, setText] = useState("");
  const [confirmingClose, setConfirmingClose] = useState(false);
  const examType = useExamType();
  const currentUser = useCurrentUser();
  const queryClient = useQueryClient();
  const bottomRef = useRef<HTMLDivElement>(null);

  const thread = useQuery({
    queryKey: ["follow-up-thread", submissionId],
    queryFn: () => getFollowUpThreadBySubmission(submissionId),
    enabled: open,
  });

  function applyResult(data: FollowUpThreadDetail) {
    queryClient.setQueryData(["follow-up-thread", submissionId], data);
    queryClient.invalidateQueries({ queryKey: ["submission", submissionId] });
    onChanged?.();
  }

  const send = useMutation({
    mutationFn: () =>
      thread.data
        ? addFollowUpMessage(thread.data.id, { userId: currentUser.data!.id, questionText: text })
        : createFollowUpThread({
            submissionId,
            userId: currentUser.data!.id,
            examTypeId: examType.data!.id,
            contextRef: null,
            questionText: text,
          }),
    onSuccess: (data) => {
      applyResult(data);
      setText("");
    },
  });

  const close = useMutation({
    mutationFn: () => closeFollowUpThread(thread.data!.id, currentUser.data!.id),
    onSuccess: (data) => {
      applyResult(data);
      setConfirmingClose(false);
    },
  });

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ block: "end" });
  }, [thread.data?.messages.length, send.isPending]);

  const isClosed = thread.data?.status === FollowUpThreadStatus.closed;

  return (
    <>
      <Button variant="outline" onClick={() => setOpen(true)}>
        <MessageCircleQuestion className="size-4" />
        对判定有异议？发起追问
      </Button>
      <SidePanel open={open} onOpenChange={setOpen}>
        <SidePanelContent width="30rem">
          <SidePanelHeader
            title="追问"
            description="说明你认为哪条判定不合理及理由，可以就同一件事多轮追问；结束追问时 AI 会综合整个对话给出最终结论，认定你正确会生成一条评分标准修正记录。"
          />
          <SidePanelBody className="flex flex-col gap-4">
            {isClosed && thread.data ? (
              <div className="flex flex-wrap items-center gap-2 rounded-lg border border-border bg-secondary/50 p-3">
                <Badge
                  variant="outline"
                  className={
                    thread.data.finalVerdict === FollowUpVerdict.user_correct
                      ? "border-transparent bg-success/12 text-success"
                      : "border-transparent bg-warning/20 text-warning-foreground"
                  }
                >
                  最终结论：
                  {thread.data.finalVerdict !== null
                    ? FollowUpVerdictLabel[thread.data.finalVerdict]
                    : "—"}
                </Badge>
                {thread.data.standardOverrideStatus !== null ? (
                  <Badge variant="outline" className="border-primary/30 text-primary">
                    标准修正：{OverrideStatusLabel[thread.data.standardOverrideStatus]}
                  </Badge>
                ) : null}
              </div>
            ) : null}

            {thread.data?.messages.map((m) => (
              <div
                key={m.id}
                className={cn(
                  "flex flex-col gap-1",
                  m.role === FollowUpMessageRole.user ? "items-end" : "items-start",
                )}
              >
                <div
                  className={cn(
                    "max-w-[85%] rounded-lg px-3 py-2 text-sm leading-relaxed",
                    m.role === FollowUpMessageRole.user
                      ? "bg-primary text-primary-foreground"
                      : "border border-border bg-secondary/50",
                  )}
                >
                  {m.content}
                </div>
                {m.role === FollowUpMessageRole.ai && m.verdict !== null ? (
                  <Badge variant="outline" className="border-border text-xs text-muted-foreground">
                    {FollowUpVerdictLabel[m.verdict]}
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
              pendingHint="AI 正在综合整个对话给出最终结论"
            />

            <div ref={bottomRef} />
          </SidePanelBody>

          {!isClosed ? (
            <SidePanelFooter className="flex-col items-stretch gap-2">
              {confirmingClose ? (
                <div className="flex items-center justify-between gap-3 rounded-lg border border-warning/40 bg-warning/10 p-3 text-sm">
                  <span>结束后不能再追问，且会依据整个对话给出最终结论，确定吗？</span>
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
                      thread.data
                        ? "继续追问……"
                        : "例如：「语域不当」这条我不认同——该表达在澳洲政府公文中是可接受的正式用法。"
                    }
                  />
                  <div className="flex items-center justify-between gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={!thread.data || send.isPending || close.isPending}
                      onClick={() => setConfirmingClose(true)}
                    >
                      结束追问
                    </Button>
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
