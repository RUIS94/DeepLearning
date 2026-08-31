import { AlertTriangle, CheckCircle2, History } from "lucide-react";
import type { WeakPoint } from "@/lib/types/dtos";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { PriorityLabel, WeakPointStatus, WeakPointStatusLabel } from "@/lib/types/enums";
import { cn } from "@/lib/utils";
import { formatDate } from "@/lib/band";

export function WeakPointCard({ weakPoint }: { weakPoint: WeakPoint }) {
  const resolved = weakPoint.status === WeakPointStatus.resolved;

  return (
    <Card className="border-border shadow-none">
      <CardContent className="flex flex-wrap items-start justify-between gap-4 p-5">
        <div className="min-w-64 flex-1 space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="text-sm font-semibold">{weakPoint.category}</h3>
            <Badge
              variant="outline"
              className={cn(
                "border-transparent",
                resolved ? "bg-success/12 text-success" : "bg-accent/15 text-accent",
              )}
            >
              {resolved ? (
                <CheckCircle2 className="size-3.5" />
              ) : (
                <AlertTriangle className="size-3.5" />
              )}
              {WeakPointStatusLabel[weakPoint.status]}
            </Badge>
            <Badge variant="outline" className="border-border text-muted-foreground">
              {PriorityLabel[weakPoint.priority]}优先级
            </Badge>
          </div>
          {weakPoint.description ? (
            <p className="text-sm leading-relaxed text-muted-foreground">{weakPoint.description}</p>
          ) : null}
          <div className="text-numeric flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground">
            <span className="inline-flex items-center gap-1">
              <History className="size-3.5" />
              首次发现 {formatDate(weakPoint.firstDetectedAt)}
            </span>
            <span>最近一次 {formatDate(weakPoint.lastSeenAt)}</span>
            <span>累计出现 {weakPoint.recurrenceCount} 次</span>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
