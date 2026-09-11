"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { WeakPointCard } from "@/components/weak-points/weak-point-card";
import { showToast } from "@/components/ui/toast";
import { SkeletonList } from "@/components/shared/skeleton-list";
import { EmptyState } from "@/components/shared/empty-state";
import { apiErrorMessage } from "@/lib/api/fetcher";
import { listWeakPoints, reclassifyWeakPoint } from "@/lib/api/weak-points";
import { listWeakPointCatalog } from "@/lib/api/exam-config";
import { WeakPointCatalogStatus } from "@/lib/types/enums";
import { useCurrentUser } from "@/hooks/use-current-user";
import { useT } from "@/lib/i18n";
import { qk } from "@/lib/query-keys";

export function WeakPointsPanel({ status }: { status: number | "all" }) {
  const t = useT();
  const currentUser = useCurrentUser();
  const queryClient = useQueryClient();

  const weakPoints = useQuery({
    queryKey: qk.weakPoints(currentUser.data?.id, status),
    queryFn: () => listWeakPoints(currentUser.data!.id, status === "all" ? undefined : status),
    enabled: !!currentUser.data,
  });

  // 薄弱点种类是全局共享的（不再按考试类型划分），这里直接拿全量清单作为可归类的目标集合。
  const catalog = useQuery({
    queryKey: qk.adminWeakPointCatalog(),
    queryFn: () => listWeakPointCatalog(),
  });
  const catalogOptions = (catalog.data ?? [])
    .filter((c) => c.status !== WeakPointCatalogStatus.deprecated)
    .map((c) => ({ id: c.id, name: c.name }));

  const [pendingId, setPendingId] = useState<string | null>(null);
  const reclassify = useMutation({
    mutationFn: ({ weakPointId, catalogId }: { weakPointId: string; catalogId: string }) =>
      reclassifyWeakPoint(weakPointId, catalogId),
    onMutate: ({ weakPointId }) => setPendingId(weakPointId),
    onSuccess: (res) => {
      showToast({
        variant: "success",
        title: res.mergedIntoExisting
          ? t("weakPoints.mergedIntoExisting")
          : t("weakPoints.classificationUpdated"),
      });
      queryClient.invalidateQueries({ queryKey: qk.weakPointsAll() });
    },
    onError: (err) =>
      showToast({
        variant: "error",
        title: t("weakPoints.classificationFailed"),
        description: apiErrorMessage(err),
      }),
    onSettled: () => setPendingId(null),
  });

  return (
    <div>
      {weakPoints.isPending ? (
        <SkeletonList count={3} itemClassName="h-28 rounded-xl" containerClassName="space-y-4" />
      ) : weakPoints.data?.length ? (
        <div className="space-y-4">
          {weakPoints.data.map((w) => (
            <WeakPointCard
              key={w.id}
              weakPoint={w}
              catalogOptions={catalogOptions}
              reclassifyPending={reclassify.isPending && pendingId === w.id}
              onReclassify={(catalogId) => reclassify.mutate({ weakPointId: w.id, catalogId })}
            />
          ))}
        </div>
      ) : (
        <EmptyState>{t("weakPoints.empty")}</EmptyState>
      )}
    </div>
  );
}
