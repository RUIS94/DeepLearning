import { AlertTriangle, CheckCircle2, History } from "lucide-react";
import type { WeakPoint } from "@/lib/types/dtos";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { PriorityLabel, WeakPointStatus, WeakPointStatusLabel } from "@/lib/types/enums";
import { cn } from "@/lib/utils";
import { formatDate } from "@/lib/band";

export function WeakPointCard({
  weakPoint,
  catalogOptions,
  onReclassify,
  reclassifyPending,
}: {
  weakPoint: WeakPoint;
  /** Active catalog kinds this weak point can be moved to. When omitted, the reclassify control is hidden. */
  catalogOptions?: { id: string; name: string }[];
  onReclassify?: (catalogId: string) => void;
  reclassifyPending?: boolean;
}) {
  const resolved = weakPoint.status === WeakPointStatus.resolved;

  return (
    <Card className="border-border shadow-none">
      <CardContent className="flex flex-wrap items-start justify-between gap-4 p-5">
        <div className="min-w-64 flex-1 space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="text-sm font-semibold">{weakPoint.label}</h3>
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
          {weakPoint.patternSummary ? (
            <p className="text-sm leading-relaxed text-muted-foreground">{weakPoint.patternSummary}</p>
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

        {catalogOptions?.length && onReclassify ? (
          <Select
            value=""
            disabled={reclassifyPending ?? false}
            onValueChange={(catalogId) => onReclassify(catalogId)}
          >
            <SelectTrigger className="h-8 w-40 text-xs">
              <SelectValue placeholder="重新归类到…" />
            </SelectTrigger>
            <SelectContent>
              {catalogOptions.map((o) => (
                <SelectItem key={o.id} value={o.id} className="text-xs">
                  {o.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        ) : null}
      </CardContent>
    </Card>
  );
}
