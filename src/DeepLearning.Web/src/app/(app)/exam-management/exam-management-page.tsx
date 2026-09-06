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
      title="Management"
      description="Manage and Add Exam Types"
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
                <TableHead>Name</TableHead>
                <TableHead>Subject Category</TableHead>
                <TableHead>Description</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="w-24 text-right">Configuration</TableHead>
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
                  <TableCell>{e.isActive ? "Enabled" : "Disabled"}</TableCell>
                  <TableCell className="text-right">
                    <Button asChild size="sm" variant="outline">
                      <Link href={`/exam-management/${e.id}`}>
                        <Settings2 className="size-3.5" />
                        Configure
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
