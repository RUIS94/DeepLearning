import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

/**
 * A loading placeholder of N identical Skeleton rows/cards — was hand-copied at ~5 call sites,
 * each with its own count/size/container-layout (代码复用扫描_07_优化计划.md §4.3/R-W-15).
 * `containerClassName` covers both the stacked-list case (`space-y-N`) and the card-grid case
 * (`grid gap-4 sm:grid-cols-2 ...`) — different call sites genuinely use different layouts, only
 * the "N copies of one Skeleton size" part was actually duplicated.
 */
export function SkeletonList({
  count,
  itemClassName,
  containerClassName,
}: {
  count: number;
  itemClassName?: string;
  containerClassName?: string;
}) {
  return (
    <div className={containerClassName ?? "space-y-2"}>
      {Array.from({ length: count }, (_, i) => (
        <Skeleton key={i} className={cn("w-full", itemClassName)} />
      ))}
    </div>
  );
}
