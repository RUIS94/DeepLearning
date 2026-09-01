"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { ArrowRight } from "lucide-react";
import {
  CenterModal,
  CenterModalBody,
  CenterModalContent,
  CenterModalFooter,
  CenterModalHeader,
} from "@/components/ui/center-modal";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { listSubmissions } from "@/lib/api/submissions";
import { SubmissionStatusLabel } from "@/lib/types/enums";
import { formatDate } from "@/lib/band";

/** 「打开做过的记录」—— 列出当前用户对某题的历史提交,点进去看当次批改结果。 */
export function QuestionRecordsModal({
  questionId,
  questionTitle,
  userId,
  open,
  onOpenChange,
}: {
  questionId: string | null;
  questionTitle: string;
  userId: string | undefined;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const records = useQuery({
    queryKey: ["submissions", userId, questionId],
    queryFn: () => listSubmissions(userId!, questionId!),
    enabled: open && !!userId && !!questionId,
  });

  return (
    <CenterModal open={open} onOpenChange={onOpenChange}>
      <CenterModalContent width="34rem">
        <CenterModalHeader title="做过的记录" description={questionTitle} />
        <CenterModalBody>
          {records.isPending ? (
            <div className="space-y-2">
              {[0, 1, 2].map((i) => (
                <Skeleton key={i} className="h-14 w-full rounded-lg" />
              ))}
            </div>
          ) : (records.data ?? []).length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">还没有提交记录。</p>
          ) : (
            <ul className="space-y-2">
              {(records.data ?? []).map((s) => (
                <li key={s.id}>
                  <Link
                    href={`/submissions/${s.id}`}
                    onClick={() => onOpenChange(false)}
                    className="flex items-center justify-between gap-3 rounded-lg border border-border px-4 py-3 text-sm transition-colors hover:bg-secondary"
                  >
                    <span className="flex items-center gap-2">
                      <Badge variant="outline">{SubmissionStatusLabel[s.status] ?? s.status}</Badge>
                      <span className="text-xs text-muted-foreground">
                        {formatDate(s.submittedAt ?? s.createdAt)}
                      </span>
                    </span>
                    <ArrowRight className="size-4 text-muted-foreground" />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </CenterModalBody>
        <CenterModalFooter>
          <Button variant="outline" size="sm" onClick={() => onOpenChange(false)}>
            关闭
          </Button>
        </CenterModalFooter>
      </CenterModalContent>
    </CenterModal>
  );
}
