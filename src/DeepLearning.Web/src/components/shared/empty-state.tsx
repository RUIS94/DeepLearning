import type { ReactNode } from "react";

/**
 * The dashed-border "nothing here yet" placeholder — was the exact same className string
 * hand-copied at ~6 call sites (代码复用扫描_07_优化计划.md §4.3/R-W-15).
 */
export function EmptyState({ children }: { children: ReactNode }) {
  return (
    <p className="rounded-lg border border-dashed border-border p-12 text-center text-sm text-muted-foreground">
      {children}
    </p>
  );
}
