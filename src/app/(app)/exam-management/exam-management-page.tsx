"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Settings2 } from "lucide-react";
import { PageShell } from "@/components/shell/page-shell";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { ErrorBanner } from "@/components/shared/ai-loading-state";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { listExamTypes } from "@/lib/api/exam-config";
import { SubjectCategoryLabel } from "@/lib/types/enums";

export function ExamManagementPage() {
  const examTypes = useQuery({ queryKey: ["admin", "exam-types"], queryFn: listExamTypes });

  return (
    <PageShell
      title="考试管理"
      description="管理考试类型"
    >
      {examTypes.isPending ? (
        <Skeleton className="h-48 w-full rounded-xl" />
      ) : examTypes.error ? (
        <ErrorBanner error={examTypes.error} />
      ) : (
        <div className="overflow-x-auto rounded-xl border border-border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Code</TableHead>
                <TableHead>名称</TableHead>
                <TableHead>学科类别</TableHead>
                <TableHead>描述</TableHead>
                <TableHead>状态</TableHead>
                <TableHead className="w-24 text-right">配置</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(examTypes.data ?? []).map((e) => (
                <TableRow key={e.id}>
                  <TableCell className="text-numeric font-mono text-xs">{e.code}</TableCell>
                  <TableCell>{e.name}</TableCell>
                  <TableCell>
                    <Badge variant="outline">{SubjectCategoryLabel[e.subjectCategory]}</Badge>
                  </TableCell>
                  <TableCell className="max-w-md text-sm text-muted-foreground">
                    {e.description ?? "—"}
                  </TableCell>
                  <TableCell>{e.isActive ? "启用" : "停用"}</TableCell>
                  <TableCell className="text-right">
                    <Button asChild size="sm" variant="outline">
                      <Link href={`/exam-management/${e.id}`}>
                        <Settings2 className="size-3.5" />
                        配置
                      </Link>
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </PageShell>
  );
}
