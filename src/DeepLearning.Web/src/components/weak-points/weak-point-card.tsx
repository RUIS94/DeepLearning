"use client";

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
import { WeakPointStatus } from "@/lib/types/enums";
import { useT } from "@/lib/i18n";
import { useEnumLabels } from "@/lib/i18n/enum-labels";
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
  const t = useT();
  const { WeakPointStatusLabel, PriorityLabel } = useEnumLabels();
  const resolved = weakPoint.status === WeakPointStatus.resolved;

  return (
    <Card className="border-border shadow-none">
      <CardContent className="flex flex-wrap items-start justify-between gap-4 p-5">
        <div className="min-w-64 flex-1 space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="text-sm font-semibold">
              {weakPoint.label ?? t("weakPoints.uncategorized")}
            </h3>
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
              {t("weakPointCard.priority", { priority: PriorityLabel[weakPoint.priority] ?? "" })}
            </Badge>
          </div>
          {weakPoint.patternSummary ? (
            <p className="text-sm leading-relaxed text-muted-foreground">
              {weakPoint.patternSummary}
            </p>
          ) : null}
          <div className="text-numeric flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground">
            <span className="inline-flex items-center gap-1">
              <History className="size-3.5" />
              {t("weakPointCard.firstDetected", { date: formatDate(weakPoint.firstDetectedAt) })}
            </span>
            <span>{t("weakPointCard.lastSeen", { date: formatDate(weakPoint.lastSeenAt) })}</span>
            <span>{t("weakPointCard.recurrence", { count: weakPoint.recurrenceCount })}</span>
          </div>
        </div>

        {catalogOptions?.length && onReclassify ? (
          <Select
            value=""
            disabled={reclassifyPending ?? false}
            onValueChange={(catalogId) => onReclassify(catalogId)}
          >
            <SelectTrigger className="h-8 w-40 text-xs">
              <SelectValue placeholder={t("weakPointCard.reclassifyPlaceholder")} />
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
