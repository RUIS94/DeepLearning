"use client";

import { Badge } from "@/components/ui/badge";
import { Difficulty } from "@/lib/types/enums";
import { useEnumLabels } from "@/lib/i18n/enum-labels";
import { cn } from "@/lib/utils";

export function DifficultyBadge({ difficulty }: { difficulty: number }) {
  const { DifficultyLabel } = useEnumLabels();
  return (
    <Badge
      variant="outline"
      className={cn(
        "border-transparent",
        difficulty === Difficulty.easy && "bg-success/12 text-success",
        difficulty === Difficulty.medium && "bg-warning/20 text-warning-foreground",
        difficulty === Difficulty.hard && "bg-destructive/12 text-destructive",
      )}
    >
      {DifficultyLabel[difficulty]}
    </Badge>
  );
}

export function TaskTypeBadge({ taskType }: { taskType: number }) {
  const { TaskTypeLabel } = useEnumLabels();
  return (
    <Badge variant="outline" className="border-border bg-secondary text-secondary-foreground">
      {TaskTypeLabel[taskType]}
    </Badge>
  );
}
