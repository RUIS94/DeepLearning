"use client";

import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { MessageCircleQuestion } from "lucide-react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { AiLoadingState } from "@/components/shared/ai-loading-state";
import { createFollowUp } from "@/lib/api/follow-ups";
import { useExamType } from "@/hooks/use-exam-config";
import { useCurrentUser } from "@/hooks/use-current-user";
import { FollowUpVerdict, FollowUpVerdictLabel, OverrideStatusLabel } from "@/lib/types/enums";
import type { FollowUpQuestionResult } from "@/lib/types/dtos";

export function FollowUpDialog({
  submissionId,
  onResolved,
}: {
  submissionId: string;
  onResolved?: (result: FollowUpQuestionResult) => void;
}) {
  const [open, setOpen] = useState(false);
  const [text, setText] = useState("");
  const [result, setResult] = useState<FollowUpQuestionResult | null>(null);
  const examType = useExamType();
  const currentUser = useCurrentUser();

  const mutation = useMutation({
    mutationFn: () =>
      createFollowUp({
        submissionId,
        userId: currentUser.data!.id,
        examTypeId: examType.data!.id,
        contextRef: null,
        questionText: text,
      }),
    onSuccess: (data) => {
      setResult(data);
      onResolved?.(data);
    },
  });

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) {
          setResult(null);
          setText("");
          mutation.reset();
        }
      }}
    >
      <DialogTrigger asChild>
        <Button variant="outline">
          <MessageCircleQuestion className="size-4" />
          对判定有异议？发起追问
        </Button>
      </DialogTrigger>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>发起追问</DialogTitle>
          <DialogDescription>
            说明你认为哪条判定不合理及理由。若 AI 复核认定你正确，会生成一条评分标准修正记录。
          </DialogDescription>
        </DialogHeader>

        {result ? (
          <div className="space-y-3">
            <div className="flex flex-wrap items-center gap-2">
              <Badge
                variant="outline"
                className={
                  result.verdict === FollowUpVerdict.user_correct
                    ? "border-transparent bg-success/12 text-success"
                    : "border-transparent bg-warning/20 text-warning-foreground"
                }
              >
                {FollowUpVerdictLabel[result.verdict]}
              </Badge>
              {result.standardOverrideStatus !== null ? (
                <Badge variant="outline" className="border-primary/30 text-primary">
                  标准修正：{OverrideStatusLabel[result.standardOverrideStatus]}
                </Badge>
              ) : null}
            </div>
            <p className="rounded-lg border border-border bg-secondary/50 p-4 text-sm leading-relaxed">
              {result.aiResponse}
            </p>
          </div>
        ) : (
          <div className="space-y-3">
            <Textarea
              value={text}
              onChange={(e) => setText(e.target.value)}
              rows={5}
              placeholder="例如：「语域不当」这条我不认同——该表达在澳洲政府公文中是可接受的正式用法。"
            />
            <AiLoadingState
              status={mutation.status}
              error={mutation.error}
              pendingHint="AI 正在复核你的追问，可能需要几秒到十几秒"
            />
          </div>
        )}

        <DialogFooter>
          {result ? (
            <Button onClick={() => setOpen(false)}>知道了</Button>
          ) : (
            <Button
              disabled={
                mutation.isPending || text.trim().length < 5 || !examType.data || !currentUser.data
              }
              onClick={() => mutation.mutate()}
            >
              {mutation.isPending ? "复核中…" : "提交追问"}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
