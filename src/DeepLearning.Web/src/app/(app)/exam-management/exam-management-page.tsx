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
                <TableHead>{t("common.code")}</TableHead>
                <TableHead>{t("common.name")}</TableHead>
                <TableHead>{t("examMgmt.col.subjectCategory")}</TableHead>
                <TableHead>{t("common.description")}</TableHead>
                <TableHead>{t("common.status")}</TableHead>
                <TableHead className="w-24 text-right">{t("examMgmt.col.configuration")}</TableHead>
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
                  <TableCell>{e.isActive ? t("common.enabled") : t("common.disabled")}</TableCell>
                  <TableCell className="text-right">
                    <Button asChild size="sm" variant="outline">
                      <Link href={`/exam-management/${e.id}`}>
                        <Settings2 className="size-3.5" />
                        {t("examMgmt.configure")}
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
