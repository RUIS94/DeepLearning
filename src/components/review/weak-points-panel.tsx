"use client";

import { useQuery } from "@tanstack/react-query";
import { WeakPointCard } from "@/components/weak-points/weak-point-card";
import { Skeleton } from "@/components/ui/skeleton";
import { listWeakPoints } from "@/lib/api/weak-points";
import { useCurrentUser } from "@/hooks/use-current-user";

export function WeakPointsPanel({ status }: { status: number | "all" }) {
  const currentUser = useCurrentUser();

  const weakPoints = useQuery({
    queryKey: ["weak-points", currentUser.data?.id, status],
    queryFn: () => listWeakPoints(currentUser.data!.id, status === "all" ? undefined : status),
    enabled: !!currentUser.data,
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
            <WeakPointCard key={w.id} weakPoint={w} />
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
