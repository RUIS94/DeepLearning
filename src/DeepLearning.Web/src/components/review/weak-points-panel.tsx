"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { WeakPointCard } from "@/components/weak-points/weak-point-card";
import { Skeleton } from "@/components/ui/skeleton";
import { showToast } from "@/components/ui/toast";
import { ApiError } from "@/lib/api/fetcher";
import { listWeakPoints, reclassifyWeakPoint } from "@/lib/api/weak-points";
import { listExamTypes, listWeakPointCatalog } from "@/lib/api/exam-config";
import { WeakPointCatalogStatus } from "@/lib/types/enums";
import { useCurrentUser } from "@/hooks/use-current-user";

export function WeakPointsPanel({ status }: { status: number | "all" }) {
  const currentUser = useCurrentUser();
  const queryClient = useQueryClient();

  const weakPointsKey = ["weak-points", currentUser.data?.id, status];
  const weakPoints = useQuery({
    queryKey: weakPointsKey,
    queryFn: () => listWeakPoints(currentUser.data!.id, status === "all" ? undefined : status),
    enabled: !!currentUser.data,
  });

  // This project has a single exam type; its catalog is the set of kinds a weak point can be moved to.
  const examTypes = useQuery({ queryKey: ["exam-types"], queryFn: () => listExamTypes() });
  const examTypeId = examTypes.data?.[0]?.id;
  const catalog = useQuery({
    queryKey: ["admin", "weak-point-catalog", examTypeId],
    queryFn: () => listWeakPointCatalog(examTypeId!),
    enabled: !!examTypeId,
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
        title: res.mergedIntoExisting ? "已并入该种类下已有的薄弱点" : "已重新归类",
      });
      queryClient.invalidateQueries({ queryKey: ["weak-points"] });
    },
    onError: (err) =>
      showToast({
        variant: "error",
        title: "归类失败",
        description: err instanceof ApiError ? (err.problem?.title ?? "") : "",
      }),
    onSettled: () => setPendingId(null),
  });

  return (
    <div>
      {weakPoints.isPending ? (
        <div className="space-y-4">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-28 w-full rounded-xl" />
          ))}
        </div>
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
        <p className="rounded-lg border border-dashed border-border p-12 text-center text-sm text-muted-foreground">
          暂无薄弱点记录，继续练习后 AI 会自动归类。
        </p>
      )}
    </div>
  );
}
