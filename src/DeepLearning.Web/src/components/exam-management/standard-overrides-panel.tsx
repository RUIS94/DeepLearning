"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Scale } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { ConfirmDialog } from "@/components/ui/confirm-dialog";
import { showToast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api/fetcher";
import {
  activateStandardOverride,
  deprecateStandardOverride,
  listStandardOverrides,
} from "@/lib/api/standard-overrides";
import { OverrideScope, OverrideStatus } from "@/lib/types/enums";
import { formatDate } from "@/lib/band";
import { cn } from "@/lib/utils";
import type { StandardOverride } from "@/lib/types/dtos";
import { useT } from "@/lib/i18n";
import { useEnumLabels } from "@/lib/i18n/enum-labels";

const statusTone: Record<number, string> = {
  [OverrideStatus.observing]: "bg-warning/20 text-warning-foreground",
  [OverrideStatus.active]: "bg-success/12 text-success",
  [OverrideStatus.deprecated]: "bg-muted text-muted-foreground",
};

export function StandardOverridesPanel({ examTypeId }: { examTypeId?: string }) {
  const t = useT();
  const { OverrideStatusLabel } = useEnumLabels();
  const scopeLabel: Record<number, string> = {
    [OverrideScope.grading_rubric]: t("examMgmt.override.scopeRubric"),
    [OverrideScope.translation_reference]: t("examMgmt.override.scopeReference"),
  };
  const queryClient = useQueryClient();
  const overrides = useQuery({
    queryKey: ["standard-overrides", examTypeId ?? null],
    // 传 examTypeId -> 该考试类型的修正 + 历史/全局(exam_type_id IS NULL)的修正。
    queryFn: () => listStandardOverrides(undefined, examTypeId),
  });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["standard-overrides"] });
  const [deprecating, setDeprecating] = useState<StandardOverride | null>(null);

  const activate = useMutation({
    mutationFn: (id: string) => activateStandardOverride(id),
    onSuccess: () => {
      showToast({ variant: "success", title: t("examMgmt.override.promoted") });
      invalidate();
    },
    onError: (err) =>
      showToast({
        variant: "error",
        title: t("examMgmt.override.promoteFailed"),
        description: err instanceof ApiError ? (err.problem?.title ?? "") : "",
      }),
  });

  return (
    <>
      <p className="mb-1 text-sm text-muted-foreground">{t("examMgmt.override.intro")}</p>
      <p className="mb-4 text-xs text-muted-foreground">{t("examMgmt.sharedNotice")}</p>

      {overrides.isPending ? (
        <div className="space-y-4">
          {[0, 1].map((i) => (
            <Skeleton key={i} className="h-24 w-full rounded-xl" />
          ))}
        </div>
      ) : overrides.data?.length ? (
        <div className="space-y-4">
          {overrides.data.map((o) => (
            <Card key={o.id} className="border-border shadow-none">
              <CardContent className="flex flex-wrap items-start justify-between gap-4 p-5">
                <div className="min-w-64 flex-1 space-y-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <Scale className="size-4 text-muted-foreground" />
                    <span className="text-sm font-medium">{scopeLabel[o.scope]}</span>
                    <Badge variant="outline" className="border-border text-muted-foreground">
                      {o.dimensionOrRule}
                    </Badge>
                    <Badge
                      variant="outline"
                      className={cn("border-transparent", statusTone[o.status])}
                    >
                      {OverrideStatusLabel[o.status]}
                    </Badge>
                    {o.examTypeId === null ? (
                      <Badge variant="outline" className="border-border text-muted-foreground">
                        {t("examMgmt.scopeGlobalBadge")}
                      </Badge>
                    ) : null}
                  </div>
                  <p className="text-sm leading-relaxed text-muted-foreground">
                    {o.revisedRuleText}
                  </p>
                  <p className="text-numeric text-xs text-muted-foreground">
                    {formatDate(o.createdAt)}
                  </p>
                </div>
                <div className="flex shrink-0 gap-2">
                  {o.status === OverrideStatus.observing ? (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={activate.isPending}
                      onClick={() => activate.mutate(o.id)}
                    >
                      {t("examMgmt.override.promote")}
                    </Button>
                  ) : null}
                  {o.status !== OverrideStatus.deprecated ? (
                    <Button
                      size="sm"
                      variant="ghost"
                      className="text-muted-foreground hover:text-destructive"
                      onClick={() => setDeprecating(o)}
                    >
                      {t("examMgmt.override.deprecate")}
                    </Button>
                  ) : null}
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      ) : (
        <p className="rounded-lg border border-dashed border-border p-12 text-center text-sm text-muted-foreground">
          {t("examMgmt.override.empty")}
        </p>
      )}

      <ConfirmDialog
        open={deprecating !== null}
        onOpenChange={(next) => {
          if (!next) setDeprecating(null);
        }}
        tone="warning"
        title={t("examMgmt.override.deprecateTitle")}
        description={t("examMgmt.override.deprecateDesc")}
        confirmLabel={t("examMgmt.override.deprecate")}
        onConfirm={async () => {
          if (!deprecating) return;
          try {
            await deprecateStandardOverride(deprecating.id);
            invalidate();
            setDeprecating(null);
          } catch (err) {
            showToast({
              variant: "error",
              title: t("examMgmt.override.deprecateFailed"),
              description: err instanceof ApiError ? (err.problem?.title ?? "") : "",
            });
            throw err;
          }
        }}
      />
    </>
  );
}
