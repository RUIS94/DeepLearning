"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Settings2 } from "lucide-react";
import { PageShell } from "@/components/shell/page-shell";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
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
import { useT } from "@/lib/i18n";
import { useEnumLabels } from "@/lib/i18n/enum-labels";

export function ExamManagementPage() {
  const t = useT();
  const { SubjectCategoryLabel } = useEnumLabels();
  const examTypes = useQuery({ queryKey: ["admin", "exam-types"], queryFn: listExamTypes });

  return (
    <PageShell title={t("nav.examManagement")} description={t("examMgmt.description")}>
      {examTypes.isPending ? (
        <Skeleton className="h-48 w-full rounded-xl" />
      ) : examTypes.error ? (
        <ErrorBanner error={examTypes.error} />
      ) : (
        <div className="overflow-x-auto rounded-xl border border-border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t("common.name")}</TableHead>
                <TableHead>{t("examMgmt.col.subjectCategory")}</TableHead>
                <TableHead>{t("common.description")}</TableHead>
                <TableHead>{t("common.status")}</TableHead>
                <TableHead className="w-16 text-right">{t("common.action")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {(examTypes.data ?? []).map((e) => (
                <TableRow key={e.id}>
                  <TableCell>{e.name}</TableCell>
                  <TableCell>{SubjectCategoryLabel[e.subjectCategory]}</TableCell>
                  <TableCell className="max-w-md text-sm text-muted-foreground">
                    {e.description ?? "—"}
                  </TableCell>
                  <TableCell
                    className={
                      e.isActive ? "font-medium text-success" : "font-medium text-destructive"
                    }
                  >
                    {e.isActive ? t("common.enabled") : t("common.disabled")}
                  </TableCell>
                  <TableCell className="text-right">
                    <TooltipProvider delayDuration={200}>
                      <Tooltip>
                        <TooltipTrigger asChild>
                          <Button asChild size="icon" variant="ghost" className="size-8">
                            <Link href={`/exam-management/${e.id}`}>
                              <Settings2 className="size-4" />
                              <span className="sr-only">{t("examMgmt.configure")}</span>
                            </Link>
                          </Button>
                        </TooltipTrigger>
                        <TooltipContent>{t("examMgmt.configure")}</TooltipContent>
                      </Tooltip>
                    </TooltipProvider>
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
